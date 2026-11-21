using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Monolith
{
    public class ControlCenterTileDef
    {
        public string Id;
        public string Glyph;
        public string NameTr;
        public string NameEn;
        public string SubTr;
        public string SubEn;
        public bool AccentDefault;
    }

    /// <summary>
    /// Windows-Taskbar-inspired customizable Control Center for Monolith.
    /// Supports 3 visual shapes (Windows 11, Compact, Glass Cards), 3 corner styles, S/M/L widths,
    /// 2 or 3 columns, 12 selectable/reorderable quick action tiles, inline Edit Mode (pencil button),
    /// real WMI brightness slider, real Core Audio master volume slider with 1-click mute,
    /// Now Playing media strip, and quick Power row.
    /// </summary>
    public class ControlCenterWindow : Window
    {
        public static readonly List<ControlCenterTileDef> AllTiles = new List<ControlCenterTileDef>
        {
            new ControlCenterTileDef { Id = "wifi",          Glyph = "\uE701", NameTr = "Wi-Fi",           NameEn = "Wi-Fi",         SubTr = "Ağ Ayarları",   SubEn = "Network",       AccentDefault = true },
            new ControlCenterTileDef { Id = "bluetooth",     Glyph = "\uE702", NameTr = "Bluetooth",       NameEn = "Bluetooth",     SubTr = "Cihazlar",      SubEn = "Devices",       AccentDefault = true },
            new ControlCenterTileDef { Id = "nightlight",    Glyph = "\uE793", NameTr = "Gece Işığı",      NameEn = "Night Light",   SubTr = "Göz Konforu",   SubEn = "Warm Screen",   AccentDefault = false },
            new ControlCenterTileDef { Id = "focus",         Glyph = "\uE708", NameTr = "Odaklanma",       NameEn = "Focus Assist",  SubTr = "Rahatsız Etme", SubEn = "Do Not Disturb",AccentDefault = false },
            new ControlCenterTileDef { Id = "display",       Glyph = "\uE7F4", NameTr = "Yansıt",          NameEn = "Project",       SubTr = "Win + P",       SubEn = "Win + P",       AccentDefault = false },
            new ControlCenterTileDef { Id = "snip",          Glyph = "\uE7A8", NameTr = "Ekran Alıntısı",  NameEn = "Snip Screen",   SubTr = "Hızlı Yakala",  SubEn = "Capture",       AccentDefault = false },
            new ControlCenterTileDef { Id = "airplane",      Glyph = "\uE709", NameTr = "Uçak Modu",       NameEn = "Airplane Mode", SubTr = "Kablosuz",      SubEn = "Wireless",      AccentDefault = false },
            new ControlCenterTileDef { Id = "sound_output",  Glyph = "\uE7F5", NameTr = "Ses Çıkışı",      NameEn = "Sound Output",  SubTr = "Hoparlör/Kul.", SubEn = "Speakers",      AccentDefault = false },
            new ControlCenterTileDef { Id = "taskview",      Glyph = "\uE7C4", NameTr = "Pencereler",      NameEn = "Task View",     SubTr = "Win + Tab",     SubEn = "Win + Tab",     AccentDefault = false },
            new ControlCenterTileDef { Id = "battery_saver", Glyph = "\uE83F", NameTr = "Pil & Güç",       NameEn = "Power & Batt.", SubTr = "Güç Modu",      SubEn = "Power Mode",    AccentDefault = false },
            new ControlCenterTileDef { Id = "taskmgr",       Glyph = "\uE9D9", NameTr = "Görev Yön.",      NameEn = "Task Manager",  SubTr = "Performans",    SubEn = "Performance",   AccentDefault = false },
            new ControlCenterTileDef { Id = "settings",      Glyph = "\uE713", NameTr = "Win Ayarları",    NameEn = "Win Settings",  SubTr = "Sistem",        SubEn = "System",        AccentDefault = false }
        };

        private readonly FrameworkElement _anchor;
        private readonly Window _ownerDock;
        private readonly MonolithConfig _cfg;
        private readonly ThemePalette _palette;
        private readonly SystemTelemetry _tel;
        private readonly Action _onChanged;
        private readonly Action _onOpenSettings;

        private Border _shell;
        private bool _editMode = false;
        private bool _suppressDeactivate = false;

        public ControlCenterWindow(
            FrameworkElement anchor,
            Window ownerDock,
            MonolithConfig cfg,
            ThemePalette palette,
            SystemTelemetry tel,
            Action onChanged,
            Action onOpenSettings)
        {
            _anchor = anchor;
            _ownerDock = ownerDock;
            _cfg = cfg;
            _palette = palette;
            _tel = tel;
            _onChanged = onChanged;
            _onOpenSettings = onOpenSettings;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;

            _shell = new Border
            {
                Background = ThemeManager.Brush(palette.ModalBg),
                BorderBrush = ThemeManager.Brush(palette.ModalBorder),
                BorderThickness = new Thickness(1.1),
                Margin = new Thickness(12),
                Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 8, Opacity = 0.54, Color = Colors.Black }
            };
            Content = _shell;

            RebuildUI();

            Deactivated += delegate
            {
                if (!_suppressDeactivate)
                {
                    try { Close(); } catch { }
                }
            };
            PreviewKeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Escape)
                {
                    if (_editMode) { _editMode = false; RebuildUI(); }
                    else Close();
                }
            };
            Loaded += delegate { PositionAtAnchor(); };
            SizeChanged += delegate { if (IsLoaded) PositionAtAnchor(); };
        }

        private double GetOuterRadius()
        {
            if (_cfg.CcCornerStyle == "Sharp") return 10;
            if (_cfg.CcCornerStyle == "Pill") return 26;
            return 18;
        }

        private double GetTileRadius()
        {
            if (_cfg.CcCornerStyle == "Sharp") return 6;
            if (_cfg.CcCornerStyle == "Pill") return 17;
            return 11;
        }

        private double GetPanelWidth()
        {
            if (_cfg.CcSize == "S") return 326;
            if (_cfg.CcSize == "L") return 420;
            return 372;
        }

        private void SaveAndRebuild()
        {
            ConfigStore.Save(_cfg);
            RebuildUI();
            if (_onChanged != null) _onChanged();
        }

        private void PositionAtAnchor()
        {
            try
            {
                if (_anchor == null || _ownerDock == null) return;
                Point pt = _anchor.PointToScreen(new Point(_anchor.ActualWidth / 2.0, 0));
                PresentationSource src = PresentationSource.FromVisual(_ownerDock);
                double dpiX = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformToDevice.M11 : 1.0;

                double cx = pt.X / dpiX;
                double left = cx - ActualWidth / 2.0;
                double sw = SystemParameters.PrimaryScreenWidth;
                if (left < 6) left = 6;
                if (left + ActualWidth > sw - 6) left = sw - ActualWidth - 6;

                Left = left;
                Top = _cfg.IsTop ? (_ownerDock.Top + _ownerDock.ActualHeight + 2) : (_ownerDock.Top - ActualHeight + 4);
            }
            catch { }
        }

        private void RebuildUI()
        {
            double outerR = GetOuterRadius();
            double tileR = GetTileRadius();
            bool compact = (_cfg.CcStyle == "Compact");
            bool glassCards = (_cfg.CcStyle == "GlassCards");

            _shell.Width = GetPanelWidth();
            _shell.CornerRadius = new CornerRadius(outerR);
            _shell.Padding = compact ? new Thickness(12, 11, 12, 10) : new Thickness(14, 14, 14, 11);

            StackPanel root = new StackPanel();

            // 1. Quick Action Tiles Grid (Windows 11 3-col or 2-col customizable grid)
            int cols = _cfg.CcColumns;
            if (glassCards && cols > 2) cols = 2;
            if (cols < 2) cols = 2;
            if (cols > 3) cols = 3;

            List<string> activeIds = _cfg.CcActiveTiles ?? new List<string>();
            if (activeIds.Count > 0)
            {
                Grid tilesGrid = new Grid { Margin = new Thickness(0, 0, 0, compact ? 8 : 11) };
                for (int c = 0; c < cols; c++)
                    tilesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                int rows = (int)Math.Ceiling(activeIds.Count / (double)cols);
                for (int r = 0; r < rows; r++)
                    tilesGrid.RowDefinitions.Add(new RowDefinition());

                for (int i = 0; i < activeIds.Count; i++)
                {
                    ControlCenterTileDef def = FindTileDef(activeIds[i]);
                    if (def == null) continue;
                    int r = i / cols;
                    int c = i % cols;
                    UIElement tileEl = BuildQuickTile(def, i, activeIds.Count, r, c, cols, tileR, compact, glassCards);
                    Grid.SetRow(tileEl, r);
                    Grid.SetColumn(tileEl, c);
                    tilesGrid.Children.Add(tileEl);
                }
                root.Children.Add(tilesGrid);
            }

            // 2. Inline Edit / Shape Drawer (Windows 11 Quick Settings Pencil mode!)
            if (_editMode)
            {
                root.Children.Add(BuildInlineCustomizer(tileR));
            }

            // 3. Real Screen Brightness Slider (WMI - laptops)
            if (_cfg.CcShowBrightness)
            {
                int curBright;
                bool hasBright = NativeWin32.TryGetScreenBrightness(out curBright);
                if (hasBright)
                {
                    root.Children.Add(BuildSliderRow(
                        "\uE706",
                        L10n.T(_cfg, "Parlaklık", "Brightness"),
                        curBright,
                        tileR,
                        compact,
                        null,
                        delegate(int val) { NativeWin32.SetScreenBrightness(val); }));
                }
            }

            // 4. Real Windows Master Volume Slider (clickable icon toggles Mute!)
            if (_cfg.CcShowVolume)
            {
                root.Children.Add(BuildSliderRow(
                    _tel.VolumeMuted ? "\uE74F" : "\uE767",
                    L10n.T(_cfg, "Ses", "Volume"),
                    _tel.VolumePercent,
                    tileR,
                    compact,
                    delegate
                    {
                        NativeWin32.SendKeyPress(NativeWin32.VK_VOLUME_MUTE);
                        _tel.VolumeMuted = !_tel.VolumeMuted;
                        RebuildUI();
                        if (_onChanged != null) _onChanged();
                    },
                    delegate(int val)
                    {
                        NativeWin32.SetSystemVolume(val);
                        _tel.VolumePercent = val;
                        _tel.VolumeMuted = (val == 0);
                        if (_onChanged != null) _onChanged();
                    }));
            }

            // 5. Now Playing Mini Card
            if (_cfg.CcShowMedia)
            {
                Border mediaCard = new Border
                {
                    CornerRadius = new CornerRadius(tileR),
                    Background = ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                    BorderThickness = new Thickness(1),
                    Padding = compact ? new Thickness(9, 6, 9, 6) : new Thickness(11, 8, 11, 8),
                    Margin = new Thickness(0, 0, 0, compact ? 7 : 10)
                };
                DockPanel mp = new DockPanel();
                StackPanel mediaBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                mediaBtns.Children.Add(MakeRoundBtn("\uE892", delegate { NativeWin32.SendKeyPress(NativeWin32.VK_MEDIA_PREV_TRACK); }));
                mediaBtns.Children.Add(MakeRoundBtn("\uE768", delegate { NativeWin32.SendKeyPress(NativeWin32.VK_MEDIA_PLAY_PAUSE); }));
                mediaBtns.Children.Add(MakeRoundBtn("\uE893", delegate { NativeWin32.SendKeyPress(NativeWin32.VK_MEDIA_NEXT_TRACK); }));
                DockPanel.SetDock(mediaBtns, Dock.Right);
                mp.Children.Add(mediaBtns);

                StackPanel mInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                mInfo.Children.Add(new TextBlock
                {
                    Text = _tel.Media.HasMedia ? _tel.Media.Title : L10n.T(_cfg, "Medya Hazır", "Ready to Play"),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.8,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                if (!compact)
                {
                    mInfo.Children.Add(new TextBlock
                    {
                        Text = _tel.Media.SourceApp,
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 10,
                        Foreground = ThemeManager.Brush(_palette.TextMuted)
                    });
                }
                mp.Children.Add(mInfo);
                mediaCard.Child = mp;
                root.Children.Add(mediaCard);
            }

            // 6. Power Row
            if (_cfg.CcShowPowerRow)
            {
                Grid pwr = new Grid { Margin = new Thickness(0, 0, 0, 9) };
                for (int i = 0; i < 4; i++) pwr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                AddPowerBtn(pwr, 0, "\uE72E", L10n.T(_cfg, "Kilitle", "Lock"), false, tileR, compact, delegate { Close(); NativeWin32.LockWorkStation(); });
                AddPowerBtn(pwr, 1, "\uE708", L10n.T(_cfg, "Uyku", "Sleep"), false, tileR, compact, delegate { Close(); NativeWin32.PutComputerToSleep(); });
                AddPowerBtn(pwr, 2, "\uE777", L10n.T(_cfg, "Yeniden", "Restart"), false, tileR, compact, delegate
                {
                    _suppressDeactivate = true;
                    MessageBoxResult res = MessageBox.Show(L10n.T(_cfg, "Bilgisayar yeniden başlatılsın mı?", "Restart computer?"), "Monolith", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    _suppressDeactivate = false;
                    if (res == MessageBoxResult.Yes)
                    {
                        try { Process.Start("shutdown", "/r /t 0"); } catch { }
                    }
                });
                AddPowerBtn(pwr, 3, "\uE7E8", L10n.T(_cfg, "Kapat", "Power"), true, tileR, compact, delegate
                {
                    _suppressDeactivate = true;
                    MessageBoxResult res = MessageBox.Show(L10n.T(_cfg, "Bilgisayar kapatılsın mı?", "Shut down computer?"), "Monolith", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    _suppressDeactivate = false;
                    if (res == MessageBoxResult.Yes)
                    {
                        try { Process.Start("shutdown", "/s /t 0"); } catch { }
                    }
                });
                root.Children.Add(pwr);
            }

            // 7. Windows 11-style Bottom Footer Bar (Battery % on left, Edit Pencil + Win+A + Settings on right)
            Border footer = new Border
            {
                BorderBrush = ThemeManager.Brush(_palette.DividerColor),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(2, 8, 2, 0)
            };
            DockPanel fdp = new DockPanel { VerticalAlignment = VerticalAlignment.Center };

            // Left: Battery readout
            StackPanel battSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            battSp.Children.Add(new TextBlock
            {
                Text = _tel.HasBattery ? "\uE83F" : "\uE945",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12.5,
                Foreground = ThemeManager.Brush(_palette.Accent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            battSp.Children.Add(new TextBlock
            {
                Text = _tel.HasBattery ? (_tel.BatteryPercent + "%") : L10n.T(_cfg, "Güç Bağlı", "AC Power"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(_palette.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center
            });
            fdp.Children.Add(battSp);

            // Right: Edit Pencil + Win Quick Settings + Monolith Settings
            StackPanel fBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

            Border editBtn = MakeFooterIconBtn("\uE70F", _editMode, L10n.T(_cfg, "Kontrol Merkezini Şekillendir", "Customize Control Center"), delegate
            {
                _editMode = !_editMode;
                RebuildUI();
            });
            fBtns.Children.Add(editBtn);

            Border winQuickBtn = MakeFooterIconBtn("\uE71D", false, L10n.T(_cfg, "Windows Hızlı Ayarlar (Win+A)", "Windows Quick Settings (Win+A)"), delegate
            {
                Close();
                NativeWin32.OpenWindowsQuickSettings();
            });
            fBtns.Children.Add(winQuickBtn);

            Border setBtn = MakeFooterIconBtn("\uE713", false, L10n.T(_cfg, "Monolith Ayarlar", "Monolith Settings"), delegate
            {
                Close();
                if (_onOpenSettings != null) _onOpenSettings();
            });
            fBtns.Children.Add(setBtn);

            DockPanel.SetDock(fBtns, Dock.Right);
            fdp.Children.Add(fBtns);
            footer.Child = fdp;
            root.Children.Add(footer);

            _shell.Child = root;
        }

        private UIElement BuildQuickTile(ControlCenterTileDef def, int idx, int total, int row, int col, int cols, double tileR, bool compact, bool glassCards)
        {
            double padH = (col == 0) ? 0 : 3.5;
            double padR = (col == cols - 1) ? 0 : 3.5;
            double padT = (row == 0) ? 0 : 3.5;

            string title = L10n.T(_cfg, def.NameTr, def.NameEn);
            string sub = L10n.T(_cfg, def.SubTr, def.SubEn);

            Grid host = new Grid { Margin = new Thickness(padH, padT, padR, 3.5) };

            if (glassCards)
            {
                Border card = new Border
                {
                    CornerRadius = new CornerRadius(tileR),
                    Background = ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 9, 10, 9),
                    Cursor = Cursors.Hand
                };
                StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal };
                Border badge = new Border
                {
                    Width = 30,
                    Height = 30,
                    CornerRadius = new CornerRadius(15),
                    Background = def.AccentDefault
                        ? ThemeManager.Brush(_palette.Accent)
                        : ThemeManager.Brush(Color.FromArgb(42, 148, 163, 184)),
                    Margin = new Thickness(0, 0, 9, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                badge.Child = new TextBlock
                {
                    Text = def.Glyph,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 13,
                    Foreground = def.AccentDefault ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                StackPanel txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                txt.Children.Add(new TextBlock { Text = title, FontFamily = new FontFamily("Segoe UI"), FontSize = 11.8, FontWeight = FontWeights.Bold, Foreground = ThemeManager.Brush(_palette.TextPrimary), TextTrimming = TextTrimming.CharacterEllipsis });
                txt.Children.Add(new TextBlock { Text = sub, FontFamily = new FontFamily("Segoe UI"), FontSize = 9.8, Foreground = ThemeManager.Brush(_palette.TextMuted), TextTrimming = TextTrimming.CharacterEllipsis });
                sp.Children.Add(badge);
                sp.Children.Add(txt);
                card.Child = sp;

                card.MouseEnter += delegate { card.BorderBrush = ThemeManager.Brush(_palette.Accent); };
                card.MouseLeave += delegate { card.BorderBrush = ThemeManager.Brush(_palette.ModalBorder); };
                if (!_editMode)
                {
                    string cid = def.Id;
                    card.MouseLeftButtonUp += delegate { ExecuteTileAction(cid); };
                }
                host.Children.Add(card);
            }
            else
            {
                // Authentic Windows 11 Quick Settings pill button + crisp centered caption!
                StackPanel cell = new StackPanel();
                Border pill = new Border
                {
                    Height = compact ? 38 : 44,
                    CornerRadius = new CornerRadius(tileR),
                    Background = def.AccentDefault
                        ? ThemeManager.Brush(_palette.Accent)
                        : ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = ThemeManager.Brush(def.AccentDefault ? _palette.Accent : _palette.ModalBorder),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand
                };
                pill.Child = new TextBlock
                {
                    Text = def.Glyph,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = compact ? 14 : 15.5,
                    Foreground = def.AccentDefault ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                pill.MouseEnter += delegate { pill.Opacity = 0.86; };
                pill.MouseLeave += delegate { pill.Opacity = 1.0; };
                if (!_editMode)
                {
                    string cid = def.Id;
                    pill.MouseLeftButtonUp += delegate { ExecuteTileAction(cid); };
                }
                cell.Children.Add(pill);

                cell.Children.Add(new TextBlock
                {
                    Text = title,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = compact ? 10 : 10.6,
                    FontWeight = FontWeights.Medium,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 4, 0, 1)
                });
                host.Children.Add(cell);
            }

            // Inline Edit Overlay: move left/right + remove 'x'
            if (_editMode)
            {
                Border overlay = new Border
                {
                    CornerRadius = new CornerRadius(tileR),
                    Background = ThemeManager.Brush(Color.FromArgb(140, 10, 14, 24)),
                    VerticalAlignment = VerticalAlignment.Top,
                    Height = glassCards ? 48 : (compact ? 38 : 44)
                };
                StackPanel controls = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                int capIdx = idx;
                if (idx > 0)
                    controls.Children.Add(MakeEditMiniBtn("\u25C0", false, delegate { MoveTile(capIdx, -1); }));
                if (idx < total - 1)
                    controls.Children.Add(MakeEditMiniBtn("\u25B6", false, delegate { MoveTile(capIdx, 1); }));
                controls.Children.Add(MakeEditMiniBtn("\u2715", true, delegate { RemoveTile(capIdx); }));
                overlay.Child = controls;
                host.Children.Add(overlay);
            }

            return host;
        }

        private Border BuildInlineCustomizer(double tileR)
        {
            Border panel = new Border
            {
                CornerRadius = new CornerRadius(tileR),
                Background = ThemeManager.Brush(Color.FromArgb(_palette.IsLight ? (byte)220 : (byte)155,
                    _palette.IsLight ? (byte)238 : (byte)12,
                    _palette.IsLight ? (byte)242 : (byte)16,
                    _palette.IsLight ? (byte)249 : (byte)28)),
                BorderBrush = ThemeManager.Brush(_palette.Accent),
                BorderThickness = new Thickness(1.2),
                Padding = new Thickness(10, 9, 10, 9),
                Margin = new Thickness(0, 0, 0, 10)
            };

            StackPanel sp = new StackPanel();

            DockPanel topHdr = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };
            topHdr.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, "Kontrol Merkezini Şekillendir", "Customize Control Center"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.8,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.Accent),
                VerticalAlignment = VerticalAlignment.Center
            });
            Border doneBtn = new Border
            {
                CornerRadius = new CornerRadius(7),
                Background = ThemeManager.Brush(_palette.Accent),
                Padding = new Thickness(9, 3, 9, 3),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            doneBtn.Child = new TextBlock { Text = L10n.T(_cfg, "Bitti", "Done"), FontFamily = new FontFamily("Segoe UI"), FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = Brushes.White };
            doneBtn.MouseLeftButtonUp += delegate { _editMode = false; RebuildUI(); };
            DockPanel.SetDock(doneBtn, Dock.Right);
            topHdr.Children.Add(doneBtn);
            sp.Children.Add(topHdr);

            // Style row
            WrapPanel styleRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 5) };
            styleRow.Children.Add(MakeOptionChip("Windows 11", _cfg.CcStyle == "Windows11", delegate { _cfg.CcStyle = "Windows11"; _cfg.CcColumns = 3; SaveAndRebuild(); }));
            styleRow.Children.Add(MakeOptionChip(L10n.T(_cfg, "Kompakt", "Compact"), _cfg.CcStyle == "Compact", delegate { _cfg.CcStyle = "Compact"; _cfg.CcColumns = 3; SaveAndRebuild(); }));
            styleRow.Children.Add(MakeOptionChip(L10n.T(_cfg, "Cam Kart", "Glass Cards"), _cfg.CcStyle == "GlassCards", delegate { _cfg.CcStyle = "GlassCards"; _cfg.CcColumns = 2; SaveAndRebuild(); }));
            sp.Children.Add(styleRow);

            // Corner + Size row
            WrapPanel shapeRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 5) };
            shapeRow.Children.Add(MakeOptionChip(L10n.T(_cfg, "Yumuşak", "Rounded"), _cfg.CcCornerStyle == "Rounded", delegate { _cfg.CcCornerStyle = "Rounded"; SaveAndRebuild(); }));
            shapeRow.Children.Add(MakeOptionChip(L10n.T(_cfg, "Keskin", "Sharp"), _cfg.CcCornerStyle == "Sharp", delegate { _cfg.CcCornerStyle = "Sharp"; SaveAndRebuild(); }));
            shapeRow.Children.Add(MakeOptionChip(L10n.T(_cfg, "Kapsül", "Pill"), _cfg.CcCornerStyle == "Pill", delegate { _cfg.CcCornerStyle = "Pill"; SaveAndRebuild(); }));
            shapeRow.Children.Add(MakeOptionChip("S", _cfg.CcSize == "S", delegate { _cfg.CcSize = "S"; SaveAndRebuild(); }));
            shapeRow.Children.Add(MakeOptionChip("M", _cfg.CcSize == "M", delegate { _cfg.CcSize = "M"; SaveAndRebuild(); }));
            shapeRow.Children.Add(MakeOptionChip("L", _cfg.CcSize == "L", delegate { _cfg.CcSize = "L"; SaveAndRebuild(); }));
            sp.Children.Add(shapeRow);

            // Section toggles row
            WrapPanel secRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            secRow.Children.Add(MakeOptionChip(L10n.T(_cfg, "Parlaklık", "Bright"), _cfg.CcShowBrightness, delegate { _cfg.CcShowBrightness = !_cfg.CcShowBrightness; SaveAndRebuild(); }));
            secRow.Children.Add(MakeOptionChip(L10n.T(_cfg, "Ses", "Sound"), _cfg.CcShowVolume, delegate { _cfg.CcShowVolume = !_cfg.CcShowVolume; SaveAndRebuild(); }));
            secRow.Children.Add(MakeOptionChip(L10n.T(_cfg, "Medya", "Media"), _cfg.CcShowMedia, delegate { _cfg.CcShowMedia = !_cfg.CcShowMedia; SaveAndRebuild(); }));
            secRow.Children.Add(MakeOptionChip(L10n.T(_cfg, "Güç", "Power"), _cfg.CcShowPowerRow, delegate { _cfg.CcShowPowerRow = !_cfg.CcShowPowerRow; SaveAndRebuild(); }));
            sp.Children.Add(secRow);

            // Add missing quick action tiles
            WrapPanel addRow = new WrapPanel();
            int addedCount = 0;
            for (int i = 0; i < AllTiles.Count; i++)
            {
                ControlCenterTileDef td = AllTiles[i];
                if (_cfg.CcActiveTiles.Contains(td.Id)) continue;
                addedCount++;
                string capId = td.Id;
                addRow.Children.Add(MakeOptionChip("+ " + L10n.T(_cfg, td.NameTr, td.NameEn), false, delegate
                {
                    if (!_cfg.CcActiveTiles.Contains(capId))
                    {
                        _cfg.CcActiveTiles.Add(capId);
                        SaveAndRebuild();
                    }
                }));
            }
            if (addedCount > 0)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, "Hızlı İşlem Ekle:", "Add Quick Action:"),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    Margin = new Thickness(0, 2, 0, 4)
                });
                sp.Children.Add(addRow);
            }

            panel.Child = sp;
            return panel;
        }

        private Border MakeOptionChip(string label, bool active, Action onClick)
        {
            Border b = new Border
            {
                CornerRadius = new CornerRadius(7),
                Background = active ? ThemeManager.Brush(_palette.Accent) : ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(active ? _palette.Accent : _palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(7, 3, 7, 3),
                Margin = new Thickness(0, 0, 4, 4),
                Cursor = Cursors.Hand
            };
            b.Child = new TextBlock
            {
                Text = label,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10.2,
                FontWeight = active ? FontWeights.Bold : FontWeights.Medium,
                Foreground = active ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary)
            };
            b.MouseLeftButtonUp += delegate { onClick(); };
            return b;
        }

        private Border MakeEditMiniBtn(string glyph, bool danger, Action onClick)
        {
            Border b = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = ThemeManager.Brush(danger ? Color.FromRgb(220, 38, 38) : Color.FromArgb(200, 30, 41, 59)),
                Margin = new Thickness(2, 0, 2, 0),
                Cursor = Cursors.Hand
            };
            b.Child = new TextBlock
            {
                Text = glyph,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            b.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                onClick();
            };
            return b;
        }

        private void MoveTile(int idx, int delta)
        {
            int next = idx + delta;
            if (idx < 0 || idx >= _cfg.CcActiveTiles.Count || next < 0 || next >= _cfg.CcActiveTiles.Count) return;
            string item = _cfg.CcActiveTiles[idx];
            _cfg.CcActiveTiles.RemoveAt(idx);
            _cfg.CcActiveTiles.Insert(next, item);
            SaveAndRebuild();
        }

        private void RemoveTile(int idx)
        {
            if (idx < 0 || idx >= _cfg.CcActiveTiles.Count) return;
            _cfg.CcActiveTiles.RemoveAt(idx);
            SaveAndRebuild();
        }

        private Border BuildSliderRow(string glyph, string title, int initial, double tileR, bool compact, Action onIconClick, Action<int> onVal)
        {
            Border card = new Border
            {
                CornerRadius = new CornerRadius(tileR),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = compact ? new Thickness(10, 6, 10, 6) : new Thickness(11, 8, 11, 8),
                Margin = new Thickness(0, 0, 0, compact ? 6 : 9)
            };

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });

            Border iconBtn = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Cursor = onIconClick != null ? Cursors.Hand : Cursors.Arrow,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            iconBtn.Child = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 13.5,
                Foreground = ThemeManager.Brush(_palette.Accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (onIconClick != null)
            {
                iconBtn.MouseEnter += delegate { iconBtn.Background = ThemeManager.Brush(Color.FromArgb(40, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B)); };
                iconBtn.MouseLeave += delegate { iconBtn.Background = Brushes.Transparent; };
                iconBtn.MouseLeftButtonUp += delegate { onIconClick(); };
            }
            Grid.SetColumn(iconBtn, 0);
            g.Children.Add(iconBtn);

            Slider sl = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = initial,
                IsSnapToTickEnabled = true,
                TickFrequency = 1,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 6, 0),
                ToolTip = title
            };
            Grid.SetColumn(sl, 1);
            g.Children.Add(sl);

            TextBlock pct = new TextBlock
            {
                Text = initial + "%",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.2,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.TextSecondary),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(pct, 2);
            g.Children.Add(pct);

            sl.ValueChanged += delegate
            {
                int v = (int)Math.Round(sl.Value);
                pct.Text = v + "%";
                onVal(v);
            };

            card.Child = g;
            return card;
        }

        private Border MakeFooterIconBtn(string glyph, bool active, string tip, Action onClick)
        {
            Border b = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(8),
                Background = active ? ThemeManager.Brush(_palette.Accent) : Brushes.Transparent,
                Margin = new Thickness(4, 0, 0, 0),
                Cursor = Cursors.Hand,
                ToolTip = tip
            };
            b.Child = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12.5,
                Foreground = active ? Brushes.White : ThemeManager.Brush(_palette.TextSecondary),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            b.MouseEnter += delegate
            {
                if (!active) b.Background = ThemeManager.Brush(_palette.ModalSurface);
            };
            b.MouseLeave += delegate
            {
                if (!active) b.Background = Brushes.Transparent;
            };
            b.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                onClick();
            };
            return b;
        }

        private Border MakeRoundBtn(string glyph, Action onClick)
        {
            Border b = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = ThemeManager.Brush(Color.FromArgb(35, 148, 163, 184)),
                Margin = new Thickness(3, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            b.Child = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 11,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            b.MouseEnter += delegate { b.Background = ThemeManager.Brush(_palette.Accent); };
            b.MouseLeave += delegate { b.Background = ThemeManager.Brush(Color.FromArgb(35, 148, 163, 184)); };
            b.MouseLeftButtonUp += delegate { onClick(); };
            return b;
        }

        private void AddPowerBtn(Grid g, int col, string glyph, string lbl, bool danger, double tileR, bool compact, Action onClick)
        {
            Border b = new Border
            {
                CornerRadius = new CornerRadius(tileR),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = compact ? new Thickness(4, 5, 4, 5) : new Thickness(6, 7, 6, 7),
                Margin = new Thickness(col == 0 ? 0 : 3, 0, col == 3 ? 0 : 3, 0),
                Cursor = Cursors.Hand
            };
            StackPanel sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = compact ? 12 : 13,
                Foreground = ThemeManager.Brush(danger ? Color.FromRgb(248, 113, 113) : _palette.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = lbl,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = compact ? 9.2 : 9.8,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(_palette.TextSecondary),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2.5, 0, 0)
            });
            b.Child = sp;
            b.MouseEnter += delegate
            {
                b.Background = ThemeManager.Brush(danger
                    ? Color.FromArgb(65, 239, 68, 68)
                    : Color.FromArgb(55, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B));
            };
            b.MouseLeave += delegate { b.Background = ThemeManager.Brush(_palette.ModalSurface); };
            b.MouseLeftButtonUp += delegate { onClick(); };

            Grid.SetColumn(b, col);
            g.Children.Add(b);
        }

        private void ExecuteTileAction(string id)
        {
            if (id == "wifi") OpenUri("ms-settings:network-wifi");
            else if (id == "bluetooth") OpenUri("ms-settings:bluetooth");
            else if (id == "nightlight") OpenUri("ms-settings:nightlight");
            else if (id == "focus") OpenUri("ms-settings:quiethours");
            else if (id == "airplane") OpenUri("ms-settings:network-airplanemode");
            else if (id == "sound_output") OpenUri("ms-settings:sound");
            else if (id == "battery_saver") OpenUri("ms-settings:powersleep");
            else if (id == "settings") OpenUri("ms-settings:");
            else if (id == "snip") OpenUri("ms-screenclip:");
            else if (id == "taskmgr")
            {
                try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); } catch { }
                Close();
            }
            else if (id == "display")
            {
                Close();
                NativeWin32.OpenProjectDisplay();
            }
            else if (id == "taskview")
            {
                Close();
                NativeWin32.OpenTaskView();
            }
        }

        public static ControlCenterTileDef FindTileDef(string id)
        {
            for (int i = 0; i < AllTiles.Count; i++)
            {
                if (AllTiles[i].Id == id) return AllTiles[i];
            }
            return null;
        }

        private void OpenUri(string uri)
        {
            try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); } catch { }
            Close();
        }
    }
}
