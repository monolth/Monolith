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
    /// Windows-11-inspired customizable Control Center for Monolith.
    /// Features split-button Quick Settings tiles (Left click = instant toggle, Right '>' click = inline sub-view)
    /// for Wi-Fi, Bluetooth, Display Projection (Yansıt), and Sound Output, plus '>' on the Volume Slider row.
    /// Zero external Windows Settings windows are opened; all actions run inline via native Windows APIs.
    /// </summary>
    public class ControlCenterWindow : Window
    {
        public static readonly List<ControlCenterTileDef> AllTiles = new List<ControlCenterTileDef>
        {
            new ControlCenterTileDef { Id = "wifi",          Glyph = "\uE701", NameTr = "Wi-Fi",           NameEn = "Wi-Fi",         SubTr = "Kablosuz Ağ",   SubEn = "Wireless",      AccentDefault = true },
            new ControlCenterTileDef { Id = "bluetooth",     Glyph = "\uE702", NameTr = "Bluetooth",       NameEn = "Bluetooth",     SubTr = "Cihazlar",      SubEn = "Devices",       AccentDefault = true },
            new ControlCenterTileDef { Id = "nightlight",    Glyph = "\uE793", NameTr = "Gece Işığı",      NameEn = "Night Light",   SubTr = "Göz Konforu",   SubEn = "Warm Screen",   AccentDefault = false },
            new ControlCenterTileDef { Id = "focus",         Glyph = "\uE708", NameTr = "Odaklanma",       NameEn = "Focus Assist",  SubTr = "Rahatsız Etme", SubEn = "Do Not Disturb",AccentDefault = false },
            new ControlCenterTileDef { Id = "display",       Glyph = "\uE7F4", NameTr = "Yansıt",          NameEn = "Project",       SubTr = "Ekran Modu",    SubEn = "Display Mode",  AccentDefault = false },
            new ControlCenterTileDef { Id = "snip",          Glyph = "\uE7A8", NameTr = "Ekran Alıntısı",  NameEn = "Snip Screen",   SubTr = "Hızlı Yakala",  SubEn = "Capture",       AccentDefault = false },
            new ControlCenterTileDef { Id = "airplane",      Glyph = "\uE709", NameTr = "Uçak Modu",       NameEn = "Airplane Mode", SubTr = "Kablosuz",      SubEn = "Wireless",      AccentDefault = false },
            new ControlCenterTileDef { Id = "sound_output",  Glyph = "\uE7F5", NameTr = "Ses Çıkışı",      NameEn = "Sound Output",  SubTr = "Hoparlör/Kul.", SubEn = "Speakers",      AccentDefault = true },
            new ControlCenterTileDef { Id = "taskview",      Glyph = "\uE7C4", NameTr = "Pencereler",      NameEn = "Task View",     SubTr = "Win + Tab",     SubEn = "Win + Tab",     AccentDefault = false },
            new ControlCenterTileDef { Id = "battery_saver", Glyph = "\uE83F", NameTr = "Pil Tasarrufu",   NameEn = "Battery Saver", SubTr = "Güç Modu",      SubEn = "Power Mode",    AccentDefault = false },
            new ControlCenterTileDef { Id = "taskmgr",       Glyph = "\uE9D9", NameTr = "Görev Yön.",      NameEn = "Task Manager",  SubTr = "Performans",    SubEn = "Performance",   AccentDefault = false },
            new ControlCenterTileDef { Id = "settings",      Glyph = "\uE713", NameTr = "Ayarlar",         NameEn = "Settings",      SubTr = "Monolith",      SubEn = "Monolith",      AccentDefault = false }
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
        private string _activeSubView = null; // null, "wifi", "bluetooth", "sound_output", "display"

        // Wi-Fi state
        private bool _wifiEnabled = true;
        private bool _wifiLoading = false;
        private WifiScanResult _wifiScan = null;
        private string _expandedWifiSsid = null;
        private bool _showManualWifiInput = false;
        private string _wifiStatusMsg = null;
        private string _connectedSsidPreview = null;

        // Bluetooth state
        private bool _btEnabled = true;
        private bool _btLoading = false;
        private BluetoothScanResult _btScan = null;
        private string _btStatusMsg = null;

        // Audio output state
        private List<AudioOutputDeviceInfo> _audioDevices = new List<AudioOutputDeviceInfo>();
        private string _activeAudioShortName = null;
        private string _audioStatusMsg = null;

        // Display project state
        private string _displayStatusMsg = null;

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

            // Asynchronously populate live Wi-Fi SSID, radio states, and active Audio Output device name
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                bool wfOn = WifiManager.IsWifiEnabled();
                string ssid = wfOn ? WifiManager.GetQuickConnectedSsid() : null;
                bool btOn = BluetoothManager.IsBluetoothEnabled();
                string audName = AudioDeviceManager.GetDefaultOutputShortName();

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _wifiEnabled = wfOn;
                    _connectedSsidPreview = ssid;
                    _btEnabled = btOn;
                    _activeAudioShortName = audName;
                    if (string.IsNullOrEmpty(_activeSubView) && IsLoaded)
                        RebuildUI();
                }));
            });

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
                    if (!string.IsNullOrEmpty(_activeSubView))
                    {
                        CloseSubView();
                    }
                    else if (_editMode)
                    {
                        _editMode = false;
                        RebuildUI();
                    }
                    else
                    {
                        Close();
                    }
                }
            };
            Loaded += delegate { PositionAtAnchor(); };
            SizeChanged += delegate { if (IsLoaded) PositionAtAnchor(); };
        }

        public void OpenSubView(string subViewId)
        {
            _editMode = false;
            _activeSubView = subViewId;
            if (subViewId == "wifi")
            {
                OpenWifiSubView(true);
            }
            else if (subViewId == "bluetooth")
            {
                OpenBluetoothSubView();
            }
            else if (subViewId == "sound_output")
            {
                OpenAudioOutputSubView();
            }
            else if (subViewId == "display")
            {
                _displayStatusMsg = null;
                RebuildUI();
            }
            else
            {
                RebuildUI();
            }
        }

        private void CloseSubView()
        {
            _activeSubView = null;
            _expandedWifiSsid = null;
            _showManualWifiInput = false;
            _wifiStatusMsg = null;
            _btStatusMsg = null;
            _audioStatusMsg = null;
            _displayStatusMsg = null;
            RebuildUI();
        }

        private static bool HasSplitSubView(string id)
        {
            return id == "wifi" || id == "bluetooth" || id == "display" || id == "sound_output";
        }

        private bool IsTileActive(string id)
        {
            if (id == "wifi") return _wifiEnabled && !QuickToggleManager.IsAirplaneModeEnabled;
            if (id == "bluetooth") return _btEnabled && !QuickToggleManager.IsAirplaneModeEnabled;
            if (id == "display") return DisplayProjectManager.IsProjectionActive();
            if (id == "sound_output") return !_tel.VolumeMuted;
            if (id == "nightlight") return QuickToggleManager.IsNightLightEnabled;
            if (id == "focus") return QuickToggleManager.IsFocusModeEnabled;
            if (id == "airplane") return QuickToggleManager.IsAirplaneModeEnabled;
            if (id == "battery_saver") return QuickToggleManager.IsBatterySaverEnabled || _cfg.LowResourceMode;
            return false;
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

            // Check if an inline sub-view is currently open inside the panel
            if (_activeSubView == "wifi")
            {
                BuildWifiSubView(root, tileR, compact);
                _shell.Child = root;
                return;
            }
            if (_activeSubView == "bluetooth")
            {
                BuildBluetoothSubView(root, tileR, compact);
                _shell.Child = root;
                return;
            }
            if (_activeSubView == "sound_output")
            {
                BuildAudioOutputSubView(root, tileR, compact);
                _shell.Child = root;
                return;
            }
            if (_activeSubView == "display")
            {
                BuildDisplayProjectSubView(root, tileR, compact);
                _shell.Child = root;
                return;
            }

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
                        delegate(int val) { NativeWin32.SetScreenBrightness(val); },
                        null,
                        null));
                }
            }

            // 4. Real Windows Master Volume Slider (Left icon toggles Mute, Right '>' opens Sound Output devices sub-view!)
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
                    },
                    delegate
                    {
                        OpenSubView("sound_output");
                    },
                    L10n.T(_cfg, "Ses Çıkış Cihazını Seç", "Select Sound Output Device")));
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

            // 7. Windows 11-style Bottom Footer Bar (Battery % on left, Edit Pencil + Monolith Settings on right)
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

            // Right: Edit Pencil + Monolith Settings
            StackPanel fBtns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

            Border editBtn = MakeFooterIconBtn("\uE70F", _editMode, L10n.T(_cfg, "Kontrol Merkezini Şekillendir", "Customize Control Center"), delegate
            {
                _editMode = !_editMode;
                RebuildUI();
            });
            fBtns.Children.Add(editBtn);

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

            bool isActive = IsTileActive(def.Id);
            bool hasSubView = HasSplitSubView(def.Id);

            string title = L10n.T(_cfg, def.NameTr, def.NameEn);
            string sub = L10n.T(_cfg, def.SubTr, def.SubEn);

            if (def.Id == "wifi")
            {
                if (!_wifiEnabled || QuickToggleManager.IsAirplaneModeEnabled)
                {
                    sub = L10n.T(_cfg, "Kapalı", "Off");
                }
                else if (!string.IsNullOrEmpty(_connectedSsidPreview))
                {
                    sub = _connectedSsidPreview;
                    if (!glassCards) title = _connectedSsidPreview;
                }
            }
            else if (def.Id == "bluetooth")
            {
                sub = (_btEnabled && !QuickToggleManager.IsAirplaneModeEnabled)
                    ? L10n.T(_cfg, "Açık", "On")
                    : L10n.T(_cfg, "Kapalı", "Off");
            }
            else if (def.Id == "sound_output" && !string.IsNullOrEmpty(_activeAudioShortName))
            {
                sub = _activeAudioShortName;
            }
            else if (def.Id == "display")
            {
                string curMode = DisplayProjectManager.GetCurrentMode();
                for (int m = 0; m < DisplayProjectManager.Modes.Count; m++)
                {
                    if (DisplayProjectManager.Modes[m].ModeId == curMode)
                    {
                        sub = L10n.T(_cfg, DisplayProjectManager.Modes[m].TitleTr, DisplayProjectManager.Modes[m].TitleEn);
                        break;
                    }
                }
            }

            Grid host = new Grid { Margin = new Thickness(padH, padT, padR, 3.5) };
            string cid = def.Id;

            if (glassCards)
            {
                Border card = new Border
                {
                    CornerRadius = new CornerRadius(tileR),
                    Background = isActive
                        ? ThemeManager.Brush(Color.FromArgb(42, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B))
                        : ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = ThemeManager.Brush(isActive ? _palette.Accent : _palette.ModalBorder),
                    BorderThickness = new Thickness(1)
                };

                Grid cg = new Grid();
                cg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (hasSubView)
                {
                    cg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
                    cg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
                }

                Border leftZone = new Border
                {
                    Background = Brushes.Transparent,
                    Padding = new Thickness(10, 9, hasSubView ? 6 : 10, 9),
                    Cursor = Cursors.Hand
                };
                StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal };
                Border badge = new Border
                {
                    Width = 30,
                    Height = 30,
                    CornerRadius = new CornerRadius(15),
                    Background = isActive
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
                    Foreground = isActive ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                StackPanel txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                txt.Children.Add(new TextBlock { Text = title, FontFamily = new FontFamily("Segoe UI"), FontSize = 11.8, FontWeight = FontWeights.Bold, Foreground = ThemeManager.Brush(_palette.TextPrimary), TextTrimming = TextTrimming.CharacterEllipsis });
                txt.Children.Add(new TextBlock { Text = sub, FontFamily = new FontFamily("Segoe UI"), FontSize = 9.8, Foreground = ThemeManager.Brush(_palette.TextMuted), TextTrimming = TextTrimming.CharacterEllipsis });
                sp.Children.Add(badge);
                sp.Children.Add(txt);
                leftZone.Child = sp;

                if (!_editMode)
                {
                    leftZone.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                    {
                        e.Handled = true;
                        ExecuteTileLeftToggle(cid);
                    };
                }
                Grid.SetColumn(leftZone, 0);
                cg.Children.Add(leftZone);

                if (hasSubView)
                {
                    Border div = new Border
                    {
                        Width = 1,
                        Margin = new Thickness(0, 7, 0, 7),
                        Background = ThemeManager.Brush(_palette.ModalBorder)
                    };
                    Grid.SetColumn(div, 1);
                    cg.Children.Add(div);

                    Border rightZone = new Border
                    {
                        Background = Brushes.Transparent,
                        Cursor = Cursors.Hand,
                        ToolTip = L10n.T(_cfg, title + " Listesini Aç", "Open " + title + " List")
                    };
                    rightZone.Child = new TextBlock
                    {
                        Text = "\uE76C",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 10,
                        Foreground = ThemeManager.Brush(_palette.TextPrimary),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    rightZone.MouseEnter += delegate { rightZone.Background = ThemeManager.Brush(Color.FromArgb(40, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B)); };
                    rightZone.MouseLeave += delegate { rightZone.Background = Brushes.Transparent; };
                    if (!_editMode)
                    {
                        rightZone.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                        {
                            e.Handled = true;
                            OpenSubView(cid);
                        };
                    }
                    Grid.SetColumn(rightZone, 2);
                    cg.Children.Add(rightZone);
                }

                card.Child = cg;
                card.MouseEnter += delegate { card.BorderBrush = ThemeManager.Brush(_palette.Accent); };
                card.MouseLeave += delegate { card.BorderBrush = ThemeManager.Brush(isActive ? _palette.Accent : _palette.ModalBorder); };
                host.Children.Add(card);
            }
            else
            {
                // Authentic Windows 11 Quick Settings Split Pill Button + crisp centered caption!
                StackPanel cell = new StackPanel();
                Border pill = new Border
                {
                    Height = compact ? 38 : 44,
                    CornerRadius = new CornerRadius(tileR),
                    Background = isActive
                        ? ThemeManager.Brush(_palette.Accent)
                        : ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = ThemeManager.Brush(isActive ? _palette.Accent : _palette.ModalBorder),
                    BorderThickness = new Thickness(1)
                };

                if (hasSubView)
                {
                    Grid splitGrid = new Grid();
                    splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
                    splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(compact ? 30 : 34) });

                    // Left half: click toggles ON/OFF directly
                    Border leftBtn = new Border
                    {
                        Background = Brushes.Transparent,
                        Cursor = Cursors.Hand,
                        ToolTip = L10n.T(_cfg, title + " Aç / Kapat", "Toggle " + title)
                    };
                    leftBtn.Child = new TextBlock
                    {
                        Text = def.Glyph,
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = compact ? 14 : 15.5,
                        Foreground = isActive ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    leftBtn.MouseEnter += delegate { leftBtn.Background = ThemeManager.Brush(Color.FromArgb(28, 255, 255, 255)); };
                    leftBtn.MouseLeave += delegate { leftBtn.Background = Brushes.Transparent; };
                    if (!_editMode)
                    {
                        leftBtn.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                        {
                            e.Handled = true;
                            ExecuteTileLeftToggle(cid);
                        };
                    }
                    Grid.SetColumn(leftBtn, 0);
                    splitGrid.Children.Add(leftBtn);

                    // Center 1px vertical divider
                    Border divider = new Border
                    {
                        Width = 1,
                        Margin = new Thickness(0, 7, 0, 7),
                        Background = isActive
                            ? ThemeManager.Brush(Color.FromArgb(75, 255, 255, 255))
                            : ThemeManager.Brush(_palette.ModalBorder)
                    };
                    Grid.SetColumn(divider, 1);
                    splitGrid.Children.Add(divider);

                    // Right half ('>'): click opens inline sub-view!
                    Border rightBtn = new Border
                    {
                        Background = Brushes.Transparent,
                        Cursor = Cursors.Hand,
                        ToolTip = L10n.T(_cfg, title + " Seçenekleri", title + " Options")
                    };
                    rightBtn.Child = new TextBlock
                    {
                        Text = "\uE76C",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = compact ? 9.5 : 10.5,
                        Foreground = isActive ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    rightBtn.MouseEnter += delegate { rightBtn.Background = ThemeManager.Brush(Color.FromArgb(40, 255, 255, 255)); };
                    rightBtn.MouseLeave += delegate { rightBtn.Background = Brushes.Transparent; };
                    if (!_editMode)
                    {
                        rightBtn.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                        {
                            e.Handled = true;
                            OpenSubView(cid);
                        };
                    }
                    Grid.SetColumn(rightBtn, 2);
                    splitGrid.Children.Add(rightBtn);

                    pill.Child = splitGrid;
                }
                else
                {
                    pill.Cursor = Cursors.Hand;
                    pill.Child = new TextBlock
                    {
                        Text = def.Glyph,
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = compact ? 14 : 15.5,
                        Foreground = isActive ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    pill.MouseEnter += delegate { pill.Opacity = 0.86; };
                    pill.MouseLeave += delegate { pill.Opacity = 1.0; };
                    if (!_editMode)
                    {
                        pill.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                        {
                            e.Handled = true;
                            ExecuteTileLeftToggle(cid);
                        };
                    }
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

        private Border BuildSliderRow(string glyph, string title, int initial, double tileR, bool compact, Action onIconClick, Action<int> onVal, Action onChevronClick, string chevronTip)
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
            if (onChevronClick != null)
            {
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            }

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

            if (onChevronClick != null)
            {
                Border chevBtn = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(7),
                    Background = Brushes.Transparent,
                    Margin = new Thickness(4, 0, 0, 0),
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    ToolTip = chevronTip
                };
                chevBtn.Child = new TextBlock
                {
                    Text = "\uE76C",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 10.5,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                chevBtn.MouseEnter += delegate { chevBtn.Background = ThemeManager.Brush(Color.FromArgb(40, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B)); };
                chevBtn.MouseLeave += delegate { chevBtn.Background = Brushes.Transparent; };
                chevBtn.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    onChevronClick();
                };
                Grid.SetColumn(chevBtn, 3);
                g.Children.Add(chevBtn);
            }

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

        // =========================================================================
        // SHARED INLINE SUB-VIEW HEADER ("← Geri" + Title + Optional Toggle & Refresh)
        // =========================================================================

        private DockPanel BuildSubViewHeader(string title, bool hasToggle, bool isToggledOn, Action onToggle, Action onRefresh)
        {
            DockPanel hdr = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };

            Border backBtn = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 10, 5),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            StackPanel backSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            backSp.Children.Add(new TextBlock
            {
                Text = "\uE72B",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 11,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            });
            backSp.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, "Geri", "Back"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.2,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            });
            backBtn.Child = backSp;
            backBtn.MouseEnter += delegate { backBtn.BorderBrush = ThemeManager.Brush(_palette.Accent); };
            backBtn.MouseLeave += delegate { backBtn.BorderBrush = ThemeManager.Brush(_palette.ModalBorder); };
            backBtn.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                CloseSubView();
            };
            DockPanel.SetDock(backBtn, Dock.Left);
            hdr.Children.Add(backBtn);

            StackPanel rightControls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (hasToggle && onToggle != null)
            {
                Border togglePill = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Background = isToggledOn
                        ? ThemeManager.Brush(_palette.Accent)
                        : ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = ThemeManager.Brush(isToggledOn ? _palette.Accent : _palette.ModalBorder),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(9, 3, 9, 3),
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center
                };
                togglePill.Child = new TextBlock
                {
                    Text = isToggledOn ? L10n.T(_cfg, "Açık", "On") : L10n.T(_cfg, "Kapalı", "Off"),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = isToggledOn ? Brushes.White : ThemeManager.Brush(_palette.TextSecondary)
                };
                togglePill.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    onToggle();
                };
                rightControls.Children.Add(togglePill);
            }
            if (onRefresh != null)
            {
                Border refreshBtn = MakeFooterIconBtn("\uE72C", false, L10n.T(_cfg, "Listeyi Yenile", "Refresh List"), onRefresh);
                rightControls.Children.Add(refreshBtn);
            }
            DockPanel.SetDock(rightControls, Dock.Right);
            hdr.Children.Add(rightControls);

            hdr.Children.Add(new TextBlock
            {
                Text = title,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            return hdr;
        }

        private void AddStatusBannerIfAny(StackPanel root, string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            Border banner = new Border
            {
                CornerRadius = new CornerRadius(9),
                Background = ThemeManager.Brush(Color.FromArgb(40, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B)),
                BorderBrush = ThemeManager.Brush(_palette.Accent),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 8)
            };
            banner.Child = new TextBlock
            {
                Text = msg,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.2,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                TextWrapping = TextWrapping.Wrap
            };
            root.Children.Add(banner);
        }

        // =========================================================================
        // SUB-VIEW 1: WI-FI NETWORKS
        // =========================================================================

        private void OpenWifiSubView(bool triggerScan)
        {
            _editMode = false;
            _activeSubView = "wifi";
            _wifiLoading = true;
            RebuildUI();

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                WifiScanResult res = WifiManager.ScanNetworks(triggerScan);
                if (triggerScan && res != null && res.HasWifiAdapter && res.Networks.Count == 0)
                {
                    System.Threading.Thread.Sleep(650);
                    res = WifiManager.ScanNetworks(false);
                }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _wifiScan = res;
                    _wifiLoading = false;
                    if (res != null && !string.IsNullOrEmpty(res.ConnectedSsid))
                        _connectedSsidPreview = res.ConnectedSsid;
                    if (_activeSubView == "wifi") RebuildUI();
                }));
            });
        }

        private static string GetWifiSignalGlyph(int sig)
        {
            if (sig >= 75) return "\uE701";
            if (sig >= 50) return "\uE874";
            if (sig >= 25) return "\uE873";
            return "\uE872";
        }

        private void BuildWifiSubView(StackPanel root, double tileR, bool compact)
        {
            root.Children.Add(BuildSubViewHeader(
                "Wi-Fi",
                true,
                _wifiEnabled,
                delegate
                {
                    _wifiEnabled = !_wifiEnabled;
                    _wifiStatusMsg = _wifiEnabled
                        ? L10n.T(_cfg, "Wi-Fi açılıyor...", "Turning Wi-Fi on...")
                        : L10n.T(_cfg, "Wi-Fi kapatıldı.", "Wi-Fi turned off.");
                    RebuildUI();
                    WifiManager.SetWifiEnabledAsync(_wifiEnabled, delegate
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            if (_wifiEnabled) OpenWifiSubView(true);
                            else { _connectedSsidPreview = null; RebuildUI(); }
                        }));
                    });
                },
                delegate { OpenWifiSubView(true); }));

            AddStatusBannerIfAny(root, _wifiStatusMsg);

            if (!_wifiEnabled)
            {
                StackPanel offSp = new StackPanel { Margin = new Thickness(8, 22, 8, 22), HorizontalAlignment = HorizontalAlignment.Center };
                offSp.Children.Add(new TextBlock
                {
                    Text = "\uE701",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 24,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                offSp.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, "Wi-Fi kapalı. Ağları taramak için üstten açın.", "Wi-Fi is turned off. Turn it on above to scan networks."),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.5,
                    Foreground = ThemeManager.Brush(_palette.TextSecondary),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                });
                root.Children.Add(offSp);
                return;
            }

            if (_wifiLoading)
            {
                root.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, "Wi-Fi ağları taranıyor...", "Scanning Wi-Fi networks..."),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 26, 0, 26)
                });
                return;
            }

            if (_wifiScan == null || !_wifiScan.HasWifiAdapter)
            {
                StackPanel emptySp = new StackPanel { Margin = new Thickness(8, 18, 8, 18), HorizontalAlignment = HorizontalAlignment.Center };
                emptySp.Children.Add(new TextBlock
                {
                    Text = "\uE839",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 24,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                emptySp.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, "Wi-Fi bağdaştırıcısı bulunamadı (Ethernet kablosu aktif olabilir).", "No Wi-Fi adapter detected (Ethernet may be active)."),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.5,
                    Foreground = ThemeManager.Brush(_palette.TextSecondary),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                });
                root.Children.Add(emptySp);
            }
            else
            {
                ScrollViewer sv = new ScrollViewer
                {
                    MaxHeight = 295,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                StackPanel listSp = new StackPanel();

                if (_wifiScan.Networks.Count == 0)
                {
                    listSp.Children.Add(new TextBlock
                    {
                        Text = L10n.T(_cfg, "Yakında listelenen Wi-Fi ağı bulunamadı. Aşağıdan ağ adı (SSID) girerek doğrudan bağlanabilirsiniz.", "No nearby Wi-Fi networks listed. You can connect directly by SSID below."),
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 11.2,
                        Foreground = ThemeManager.Brush(_palette.TextMuted),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(6, 10, 6, 10)
                    });
                }

                for (int i = 0; i < _wifiScan.Networks.Count; i++)
                {
                    WifiNetworkInfo net = _wifiScan.Networks[i];
                    bool expanded = net.IsConnected || string.Equals(_expandedWifiSsid, net.Ssid, StringComparison.OrdinalIgnoreCase);

                    Border rowCard = new Border
                    {
                        CornerRadius = new CornerRadius(tileR),
                        Background = net.IsConnected
                            ? ThemeManager.Brush(Color.FromArgb(46, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B))
                            : ThemeManager.Brush(_palette.ModalSurface),
                        BorderBrush = net.IsConnected
                            ? ThemeManager.Brush(_palette.Accent)
                            : ThemeManager.Brush(_palette.ModalBorder),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(10, 8, 10, 8),
                        Margin = new Thickness(0, 0, 0, 6),
                        Cursor = Cursors.Hand
                    };

                    StackPanel cardBody = new StackPanel();

                    DockPanel topLine = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
                    StackPanel iconWrap = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
                    iconWrap.Children.Add(new TextBlock
                    {
                        Text = GetWifiSignalGlyph(net.SignalQuality),
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 15,
                        Foreground = net.IsConnected ? ThemeManager.Brush(_palette.Accent) : ThemeManager.Brush(_palette.TextPrimary),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    if (net.IsSecured)
                    {
                        iconWrap.Children.Add(new TextBlock
                        {
                            Text = "\uE72E",
                            FontFamily = new FontFamily("Segoe MDL2 Assets"),
                            FontSize = 8.5,
                            Foreground = ThemeManager.Brush(_palette.TextMuted),
                            VerticalAlignment = VerticalAlignment.Bottom,
                            Margin = new Thickness(-4, 0, 0, -2)
                        });
                    }
                    DockPanel.SetDock(iconWrap, Dock.Left);
                    topLine.Children.Add(iconWrap);

                    TextBlock sigPct = new TextBlock
                    {
                        Text = net.SignalQuality + "%",
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 10.2,
                        Foreground = ThemeManager.Brush(_palette.TextMuted),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    DockPanel.SetDock(sigPct, Dock.Right);
                    topLine.Children.Add(sigPct);

                    StackPanel nameCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                    nameCol.Children.Add(new TextBlock
                    {
                        Text = net.Ssid,
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 12.2,
                        FontWeight = net.IsConnected ? FontWeights.Bold : FontWeights.SemiBold,
                        Foreground = ThemeManager.Brush(_palette.TextPrimary),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });

                    string statusText;
                    if (net.IsConnected)
                        statusText = L10n.T(_cfg, "Bağlı", "Connected") + (net.IsSecured ? (" • " + L10n.T(_cfg, "Güvenli", "Secured")) : "");
                    else if (net.HasProfile)
                        statusText = L10n.T(_cfg, "Kayıtlı", "Saved") + (net.IsSecured ? (" • " + net.AuthLabel) : "");
                    else
                        statusText = net.IsSecured ? (L10n.T(_cfg, "Kilitli", "Secured") + " • " + net.AuthLabel) : L10n.T(_cfg, "Açık ağ", "Open network");

                    nameCol.Children.Add(new TextBlock
                    {
                        Text = statusText,
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 10.2,
                        Foreground = net.IsConnected ? ThemeManager.Brush(_palette.Accent) : ThemeManager.Brush(_palette.TextMuted),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                    topLine.Children.Add(nameCol);
                    cardBody.Children.Add(topLine);

                    if (expanded)
                    {
                        StackPanel actionArea = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
                        WifiNetworkInfo capNet = net;

                        if (net.IsConnected)
                        {
                            Border discBtn = MakeSubViewActionButton(L10n.T(_cfg, "Bağlantıyı Kes", "Disconnect"), false, delegate
                            {
                                _wifiStatusMsg = L10n.T(_cfg, "Bağlantı kesiliyor...", "Disconnecting...");
                                RebuildUI();
                                WifiManager.DisconnectAsync(delegate
                                {
                                    Dispatcher.BeginInvoke(new Action(delegate
                                    {
                                        _connectedSsidPreview = null;
                                        OpenWifiSubView(false);
                                    }));
                                });
                            });
                            actionArea.Children.Add(discBtn);
                        }
                        else if (net.HasProfile || !net.IsSecured)
                        {
                            Border connBtn = MakeSubViewActionButton(L10n.T(_cfg, "Bağlan", "Connect"), true, delegate
                            {
                                _wifiStatusMsg = L10n.T(_cfg, "Bağlanılıyor: ", "Connecting: ") + capNet.Ssid + "...";
                                RebuildUI();
                                if (capNet.HasProfile)
                                {
                                    WifiManager.ConnectSavedAsync(capNet.Ssid, capNet.ProfileName, delegate(bool ok, string msg)
                                    {
                                        Dispatcher.BeginInvoke(new Action(delegate
                                        {
                                            _wifiStatusMsg = ok
                                                ? (L10n.T(_cfg, "Bağlandı: ", "Connected: ") + capNet.Ssid)
                                                : L10n.T(_cfg, "Bağlantı kurulamadı. Tekrar deneyin.", "Could not connect. Try again.");
                                            if (ok) _connectedSsidPreview = capNet.Ssid;
                                            OpenWifiSubView(false);
                                        }));
                                    });
                                }
                                else
                                {
                                    WifiManager.ConnectWithPasswordAsync(capNet.Ssid, "", true, delegate(bool ok, string msg)
                                    {
                                        Dispatcher.BeginInvoke(new Action(delegate
                                        {
                                            _wifiStatusMsg = ok
                                                ? (L10n.T(_cfg, "Bağlandı: ", "Connected: ") + capNet.Ssid)
                                                : L10n.T(_cfg, "Bağlantı kurulamadı.", "Could not connect.");
                                            if (ok) _connectedSsidPreview = capNet.Ssid;
                                            OpenWifiSubView(false);
                                        }));
                                    });
                                }
                            });
                            actionArea.Children.Add(connBtn);
                        }
                        else
                        {
                            // New secured network -> inline password box + Connect button right inside Control Center!
                            actionArea.Children.Add(new TextBlock
                            {
                                Text = L10n.T(_cfg, "Ağ güvenlik anahtarını (şifre) girin:", "Enter network security key:"),
                                FontFamily = new FontFamily("Segoe UI"),
                                FontSize = 10.2,
                                Foreground = ThemeManager.Brush(_palette.TextSecondary),
                                Margin = new Thickness(0, 0, 0, 4)
                            });
                            PasswordBox pwdBox = new PasswordBox
                            {
                                FontFamily = new FontFamily("Segoe UI"),
                                FontSize = 12,
                                Padding = new Thickness(8, 5, 8, 5),
                                Background = ThemeManager.Brush(_palette.ModalBg),
                                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                                BorderBrush = ThemeManager.Brush(_palette.Accent),
                                BorderThickness = new Thickness(1),
                                Margin = new Thickness(0, 0, 0, 7)
                            };
                            Action submitPwd = delegate
                            {
                                string pwd = pwdBox.Password;
                                if (string.IsNullOrEmpty(pwd)) return;
                                _wifiStatusMsg = L10n.T(_cfg, "Bağlanılıyor: ", "Connecting: ") + capNet.Ssid + "...";
                                RebuildUI();
                                WifiManager.ConnectWithPasswordAsync(capNet.Ssid, pwd, false, delegate(bool ok, string msg)
                                {
                                    Dispatcher.BeginInvoke(new Action(delegate
                                    {
                                        _wifiStatusMsg = ok
                                            ? (L10n.T(_cfg, "Bağlandı: ", "Connected: ") + capNet.Ssid)
                                            : L10n.T(_cfg, "Bağlanamadı (şifreyi kontrol edin).", "Failed to connect (check password).");
                                        if (ok) { _connectedSsidPreview = capNet.Ssid; _expandedWifiSsid = null; }
                                        OpenWifiSubView(false);
                                    }));
                                });
                            };
                            pwdBox.KeyDown += delegate(object s, KeyEventArgs ke)
                            {
                                if (ke.Key == Key.Enter) { ke.Handled = true; submitPwd(); }
                            };
                            actionArea.Children.Add(pwdBox);
                            actionArea.Children.Add(MakeSubViewActionButton(L10n.T(_cfg, "Bağlan", "Connect"), true, submitPwd));
                        }

                        cardBody.Children.Add(actionArea);
                    }

                    rowCard.Child = cardBody;
                    string capSsid = net.Ssid;
                    bool capConn = net.IsConnected;
                    rowCard.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                    {
                        if (e.OriginalSource is PasswordBox) return;
                        if (!capConn && !string.Equals(_expandedWifiSsid, capSsid, StringComparison.OrdinalIgnoreCase))
                        {
                            _expandedWifiSsid = capSsid;
                            RebuildUI();
                        }
                    };
                    listSp.Children.Add(rowCard);
                }

                sv.Content = listSp;
                root.Children.Add(sv);
            }

            // Inline Manual SSID & Password Entry (zero external Windows Settings needed!)
            if (_showManualWifiInput)
            {
                Border manualBox = new Border
                {
                    CornerRadius = new CornerRadius(tileR),
                    Background = ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = ThemeManager.Brush(_palette.Accent),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 2, 0, 4)
                };
                StackPanel msp = new StackPanel();
                msp.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, "Ağ Adı (SSID) ve Şifre ile Doğrudan Bağlan:", "Connect Directly by Network Name (SSID) & Password:"),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    Margin = new Thickness(0, 0, 0, 5)
                });
                TextBox ssidInput = new TextBox
                {
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.5,
                    Padding = new Thickness(7, 4, 7, 4),
                    Background = ThemeManager.Brush(_palette.ModalBg),
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                PasswordBox pwdInput = new PasswordBox
                {
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.5,
                    Padding = new Thickness(7, 4, 7, 4),
                    Background = ThemeManager.Brush(_palette.ModalBg),
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 6)
                };
                msp.Children.Add(ssidInput);
                msp.Children.Add(pwdInput);
                msp.Children.Add(MakeSubViewActionButton(L10n.T(_cfg, "Bağlan", "Connect"), true, delegate
                {
                    string targetSsid = (ssidInput.Text ?? "").Trim();
                    string targetPwd = pwdInput.Password ?? "";
                    if (string.IsNullOrEmpty(targetSsid)) return;
                    _showManualWifiInput = false;
                    _wifiStatusMsg = L10n.T(_cfg, "Bağlanılıyor: ", "Connecting: ") + targetSsid + "...";
                    RebuildUI();
                    WifiManager.ConnectWithPasswordAsync(targetSsid, targetPwd, string.IsNullOrEmpty(targetPwd), delegate(bool ok, string msg)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            _wifiStatusMsg = ok
                                ? (L10n.T(_cfg, "Bağlandı: ", "Connected: ") + targetSsid)
                                : L10n.T(_cfg, "Bağlanamadı (Ağ adını ve şifreyi kontrol edin).", "Could not connect (check SSID and password).");
                            if (ok) _connectedSsidPreview = targetSsid;
                            OpenWifiSubView(false);
                        }));
                    });
                }));
                manualBox.Child = msp;
                root.Children.Add(manualBox);
            }
            else
            {
                Border addManualBtn = MakeSubViewActionButton(L10n.T(_cfg, "+ Gizli / Başka Ağa Bağlan", "+ Connect to Hidden Network"), false, delegate
                {
                    _showManualWifiInput = true;
                    RebuildUI();
                });
                addManualBtn.HorizontalAlignment = HorizontalAlignment.Left;
                root.Children.Add(addManualBtn);
            }
        }

        // =========================================================================
        // SUB-VIEW 2: BLUETOOTH PAIRED DEVICES
        // =========================================================================

        private void OpenBluetoothSubView()
        {
            _editMode = false;
            _activeSubView = "bluetooth";
            _btLoading = true;
            RebuildUI();

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                BluetoothScanResult res = BluetoothManager.GetPairedDevices();
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _btScan = res;
                    _btLoading = false;
                    if (_activeSubView == "bluetooth") RebuildUI();
                }));
            });
        }

        private void BuildBluetoothSubView(StackPanel root, double tileR, bool compact)
        {
            root.Children.Add(BuildSubViewHeader(
                "Bluetooth",
                true,
                _btEnabled,
                delegate
                {
                    _btEnabled = !_btEnabled;
                    _btStatusMsg = _btEnabled
                        ? L10n.T(_cfg, "Bluetooth açıldı.", "Bluetooth turned on.")
                        : L10n.T(_cfg, "Bluetooth kapatıldı.", "Bluetooth turned off.");
                    RebuildUI();
                    BluetoothManager.SetBluetoothEnabledAsync(_btEnabled, delegate
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            OpenBluetoothSubView();
                        }));
                    });
                },
                delegate { OpenBluetoothSubView(); }));

            AddStatusBannerIfAny(root, _btStatusMsg);

            if (!_btEnabled)
            {
                StackPanel offSp = new StackPanel { Margin = new Thickness(8, 22, 8, 22), HorizontalAlignment = HorizontalAlignment.Center };
                offSp.Children.Add(new TextBlock
                {
                    Text = "\uE702",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 24,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                offSp.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, "Bluetooth kapalı. Eşleştirilmiş cihazlara bağlanmak için üstten açın.", "Bluetooth is turned off. Turn it on above to connect paired devices."),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.5,
                    Foreground = ThemeManager.Brush(_palette.TextSecondary),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                });
                root.Children.Add(offSp);
                return;
            }

            if (_btLoading)
            {
                root.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, "Eşleştirilmiş Bluetooth cihazları yükleniyor...", "Loading paired Bluetooth devices..."),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 26, 0, 26)
                });
                return;
            }

            if (_btScan == null || _btScan.Devices.Count == 0)
            {
                StackPanel emptySp = new StackPanel { Margin = new Thickness(8, 20, 8, 20), HorizontalAlignment = HorizontalAlignment.Center };
                emptySp.Children.Add(new TextBlock
                {
                    Text = "\uE702",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 24,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                emptySp.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, "Eşleştirilmiş Bluetooth cihazı bulunamadı.", "No paired Bluetooth devices found."),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.5,
                    Foreground = ThemeManager.Brush(_palette.TextSecondary),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                });
                root.Children.Add(emptySp);
                return;
            }

            ScrollViewer sv = new ScrollViewer
            {
                MaxHeight = 300,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            StackPanel listSp = new StackPanel();

            for (int i = 0; i < _btScan.Devices.Count; i++)
            {
                BluetoothDeviceItem dev = _btScan.Devices[i];
                Border rowCard = new Border
                {
                    CornerRadius = new CornerRadius(tileR),
                    Background = dev.IsConnected
                        ? ThemeManager.Brush(Color.FromArgb(46, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B))
                        : ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = dev.IsConnected
                        ? ThemeManager.Brush(_palette.Accent)
                        : ThemeManager.Brush(_palette.ModalBorder),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 6),
                    Cursor = Cursors.Hand
                };

                DockPanel line = new DockPanel { VerticalAlignment = VerticalAlignment.Center };

                Border iconBadge = new Border
                {
                    Width = 30,
                    Height = 30,
                    CornerRadius = new CornerRadius(15),
                    Background = dev.IsConnected
                        ? ThemeManager.Brush(_palette.Accent)
                        : ThemeManager.Brush(Color.FromArgb(38, 148, 163, 184)),
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                iconBadge.Child = new TextBlock
                {
                    Text = dev.IconGlyph,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 13.5,
                    Foreground = dev.IsConnected ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(iconBadge, Dock.Left);
                line.Children.Add(iconBadge);

                BluetoothDeviceItem capDev = dev;
                Action toggleAction = delegate
                {
                    _btStatusMsg = capDev.IsConnected
                        ? (L10n.T(_cfg, "Bağlantı kesiliyor: ", "Disconnecting: ") + capDev.Name + "...")
                        : (L10n.T(_cfg, "Bağlanılıyor: ", "Connecting: ") + capDev.Name + "...");
                    RebuildUI();
                    BluetoothManager.ToggleDeviceConnectionAsync(capDev, delegate(bool ok, bool nowConn)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            _btStatusMsg = nowConn
                                ? (L10n.T(_cfg, "Bağlandı: ", "Connected: ") + capDev.Name)
                                : (L10n.T(_cfg, "Bağlantı kesildi: ", "Disconnected: ") + capDev.Name);
                            _activeAudioShortName = AudioDeviceManager.GetDefaultOutputShortName();
                            OpenBluetoothSubView();
                        }));
                    });
                };

                Border actionBtn = MakeSubViewActionButton(
                    dev.IsConnected ? L10n.T(_cfg, "Kes", "Disconnect") : L10n.T(_cfg, "Bağlan", "Connect"),
                    !dev.IsConnected,
                    toggleAction);
                actionBtn.VerticalAlignment = VerticalAlignment.Center;
                DockPanel.SetDock(actionBtn, Dock.Right);
                line.Children.Add(actionBtn);

                StackPanel infoCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                infoCol.Children.Add(new TextBlock
                {
                    Text = dev.Name,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    FontWeight = dev.IsConnected ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                infoCol.Children.Add(new TextBlock
                {
                    Text = (dev.IsConnected ? L10n.T(_cfg, "Bağlı", "Connected") : L10n.T(_cfg, "Eşleştirildi", "Paired"))
                           + " • " + L10n.T(_cfg, dev.CategoryTr, dev.CategoryEn),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10.2,
                    Foreground = dev.IsConnected ? ThemeManager.Brush(_palette.Accent) : ThemeManager.Brush(_palette.TextMuted),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                line.Children.Add(infoCol);

                rowCard.Child = line;
                rowCard.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    toggleAction();
                };
                listSp.Children.Add(rowCard);
            }

            sv.Content = listSp;
            root.Children.Add(sv);
        }

        // =========================================================================
        // SUB-VIEW 3: SOUND OUTPUT DEVICES (Hoparlör, Kulaklık, Monitör)
        // =========================================================================

        private void OpenAudioOutputSubView()
        {
            _editMode = false;
            _activeSubView = "sound_output";
            _audioDevices = AudioDeviceManager.GetActiveOutputDevices();
            for (int i = 0; i < _audioDevices.Count; i++)
            {
                if (_audioDevices[i].IsDefault)
                {
                    _activeAudioShortName = _audioDevices[i].ShortName;
                    break;
                }
            }
            RebuildUI();
        }

        private void BuildAudioOutputSubView(StackPanel root, double tileR, bool compact)
        {
            root.Children.Add(BuildSubViewHeader(
                L10n.T(_cfg, "Ses Çıkışı", "Sound Output"),
                false,
                true,
                null,
                delegate { OpenAudioOutputSubView(); }));

            AddStatusBannerIfAny(root, _audioStatusMsg);

            if (_audioDevices == null || _audioDevices.Count == 0)
            {
                root.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, "Aktif ses çıkış cihazı bulunamadı.", "No active sound output devices found."),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.5,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 22, 0, 22)
                });
                return;
            }

            ScrollViewer sv = new ScrollViewer
            {
                MaxHeight = 300,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            StackPanel listSp = new StackPanel();

            for (int i = 0; i < _audioDevices.Count; i++)
            {
                AudioOutputDeviceInfo dev = _audioDevices[i];
                Border rowCard = new Border
                {
                    CornerRadius = new CornerRadius(tileR),
                    Background = dev.IsDefault
                        ? ThemeManager.Brush(Color.FromArgb(46, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B))
                        : ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = dev.IsDefault
                        ? ThemeManager.Brush(_palette.Accent)
                        : ThemeManager.Brush(_palette.ModalBorder),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 9, 10, 9),
                    Margin = new Thickness(0, 0, 0, 6),
                    Cursor = Cursors.Hand
                };

                DockPanel line = new DockPanel { VerticalAlignment = VerticalAlignment.Center };

                Border iconBadge = new Border
                {
                    Width = 30,
                    Height = 30,
                    CornerRadius = new CornerRadius(15),
                    Background = dev.IsDefault
                        ? ThemeManager.Brush(_palette.Accent)
                        : ThemeManager.Brush(Color.FromArgb(38, 148, 163, 184)),
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                iconBadge.Child = new TextBlock
                {
                    Text = dev.IconGlyph,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 13.5,
                    Foreground = dev.IsDefault ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(iconBadge, Dock.Left);
                line.Children.Add(iconBadge);

                if (dev.IsDefault)
                {
                    TextBlock chk = new TextBlock
                    {
                        Text = "\uE73E",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 14,
                        Foreground = ThemeManager.Brush(_palette.Accent),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(6, 0, 2, 0)
                    };
                    DockPanel.SetDock(chk, Dock.Right);
                    line.Children.Add(chk);
                }

                StackPanel nameCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                nameCol.Children.Add(new TextBlock
                {
                    Text = dev.Name,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.8,
                    FontWeight = dev.IsDefault ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                nameCol.Children.Add(new TextBlock
                {
                    Text = dev.IsDefault
                        ? L10n.T(_cfg, "Varsayılan Ses Çıkışı", "Default Sound Output")
                        : L10n.T(_cfg, "Varsayılan yapmak için tıklayın", "Click to set as default"),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10.2,
                    Foreground = dev.IsDefault ? ThemeManager.Brush(_palette.Accent) : ThemeManager.Brush(_palette.TextMuted)
                });
                line.Children.Add(nameCol);

                rowCard.Child = line;
                AudioOutputDeviceInfo capDev = dev;
                rowCard.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    if (AudioDeviceManager.SetDefaultOutputDevice(capDev.Id))
                    {
                        _activeAudioShortName = capDev.ShortName;
                        _audioStatusMsg = L10n.T(_cfg, "Varsayılan ses cihazı: ", "Default audio output: ") + capDev.ShortName;
                        OpenAudioOutputSubView();
                    }
                };
                listSp.Children.Add(rowCard);
            }

            sv.Content = listSp;
            root.Children.Add(sv);
        }

        // =========================================================================
        // SUB-VIEW 4: DISPLAY PROJECTION (Yansıt: PC, Yinele, Uzat, İkinci Ekran)
        // =========================================================================

        private void BuildDisplayProjectSubView(StackPanel root, double tileR, bool compact)
        {
            root.Children.Add(BuildSubViewHeader(
                L10n.T(_cfg, "Yansıt", "Project"),
                false,
                true,
                null,
                null));

            AddStatusBannerIfAny(root, _displayStatusMsg);

            string curMode = DisplayProjectManager.GetCurrentMode();

            for (int i = 0; i < DisplayProjectManager.Modes.Count; i++)
            {
                ProjectionModeOption opt = DisplayProjectManager.Modes[i];
                bool isCurrent = string.Equals(opt.ModeId, curMode, StringComparison.OrdinalIgnoreCase);

                Border rowCard = new Border
                {
                    CornerRadius = new CornerRadius(tileR),
                    Background = isCurrent
                        ? ThemeManager.Brush(Color.FromArgb(46, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B))
                        : ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = isCurrent
                        ? ThemeManager.Brush(_palette.Accent)
                        : ThemeManager.Brush(_palette.ModalBorder),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(11, 9, 11, 9),
                    Margin = new Thickness(0, 0, 0, 6),
                    Cursor = Cursors.Hand
                };

                DockPanel line = new DockPanel { VerticalAlignment = VerticalAlignment.Center };

                Border iconBadge = new Border
                {
                    Width = 32,
                    Height = 32,
                    CornerRadius = new CornerRadius(16),
                    Background = isCurrent
                        ? ThemeManager.Brush(_palette.Accent)
                        : ThemeManager.Brush(Color.FromArgb(38, 148, 163, 184)),
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                iconBadge.Child = new TextBlock
                {
                    Text = opt.IconGlyph,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 14,
                    Foreground = isCurrent ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(iconBadge, Dock.Left);
                line.Children.Add(iconBadge);

                if (isCurrent)
                {
                    TextBlock chk = new TextBlock
                    {
                        Text = "\uE73E",
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 14,
                        Foreground = ThemeManager.Brush(_palette.Accent),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(6, 0, 2, 0)
                    };
                    DockPanel.SetDock(chk, Dock.Right);
                    line.Children.Add(chk);
                }

                StackPanel txtCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                txtCol.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, opt.TitleTr, opt.TitleEn),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12,
                    FontWeight = isCurrent ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary)
                });
                txtCol.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, opt.SubTr, opt.SubEn),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10.2,
                    Foreground = isCurrent ? ThemeManager.Brush(_palette.Accent) : ThemeManager.Brush(_palette.TextMuted)
                });
                line.Children.Add(txtCol);

                rowCard.Child = line;
                ProjectionModeOption capOpt = opt;
                rowCard.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    DisplayProjectManager.ApplyProjectionMode(capOpt.ModeId);
                    _displayStatusMsg = L10n.T(_cfg, "Uygulandı: ", "Applied: ") + L10n.T(_cfg, capOpt.TitleTr, capOpt.TitleEn);
                    RebuildUI();
                };
                root.Children.Add(rowCard);
            }
        }

        private Border MakeSubViewActionButton(string label, bool primary, Action onClick)
        {
            Border b = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = primary ? ThemeManager.Brush(_palette.Accent) : ThemeManager.Brush(Color.FromArgb(35, 148, 163, 184)),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            b.Child = new TextBlock
            {
                Text = label,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.2,
                FontWeight = FontWeights.SemiBold,
                Foreground = primary ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary)
            };
            b.MouseEnter += delegate { b.Opacity = 0.88; };
            b.MouseLeave += delegate { b.Opacity = 1.0; };
            b.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                onClick();
            };
            return b;
        }

        // =========================================================================
        // LEFT-CLICK TILE TOGGLE ACTIONS (Zero external Windows Settings windows!)
        // =========================================================================

        private void ExecuteTileLeftToggle(string id)
        {
            if (id == "wifi")
            {
                _wifiEnabled = !_wifiEnabled;
                if (!_wifiEnabled) _connectedSsidPreview = null;
                RebuildUI();
                WifiManager.SetWifiEnabledAsync(_wifiEnabled, delegate
                {
                    if (_wifiEnabled)
                    {
                        string ssid = WifiManager.GetQuickConnectedSsid();
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            _connectedSsidPreview = ssid;
                            if (string.IsNullOrEmpty(_activeSubView)) RebuildUI();
                        }));
                    }
                });
            }
            else if (id == "bluetooth")
            {
                _btEnabled = !_btEnabled;
                RebuildUI();
                BluetoothManager.SetBluetoothEnabledAsync(_btEnabled, null);
            }
            else if (id == "display")
            {
                DisplayProjectManager.ToggleProjection();
                RebuildUI();
            }
            else if (id == "sound_output")
            {
                NativeWin32.SendKeyPress(NativeWin32.VK_VOLUME_MUTE);
                _tel.VolumeMuted = !_tel.VolumeMuted;
                RebuildUI();
                if (_onChanged != null) _onChanged();
            }
            else if (id == "nightlight")
            {
                QuickToggleManager.ToggleNightLight();
                RebuildUI();
            }
            else if (id == "focus")
            {
                QuickToggleManager.ToggleFocusMode();
                RebuildUI();
            }
            else if (id == "airplane")
            {
                QuickToggleManager.ToggleAirplaneModeAsync(delegate
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        _wifiEnabled = !QuickToggleManager.IsAirplaneModeEnabled;
                        _btEnabled = !QuickToggleManager.IsAirplaneModeEnabled;
                        RebuildUI();
                    }));
                });
                RebuildUI();
            }
            else if (id == "battery_saver")
            {
                QuickToggleManager.ToggleBatterySaver(_cfg);
                RebuildUI();
                if (_onChanged != null) _onChanged();
            }
            else if (id == "settings")
            {
                Close();
                if (_onOpenSettings != null) _onOpenSettings();
            }
            else if (id == "snip")
            {
                Close();
                try
                {
                    Process.Start(new ProcessStartInfo("snippingtool.exe", "/clip") { UseShellExecute = true });
                }
                catch { }
            }
            else if (id == "taskmgr")
            {
                Close();
                try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); } catch { }
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
    }
}
