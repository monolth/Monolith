using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace Monolith
{
    /// <summary>
    /// Monolith-style Settings floating frosted-glass modal with tabs:
    /// Appearance (6 Themes + Clear Card Style + 9 Accents + Glass Intensity),
    /// Dock (S/M/L, Top/Bottom, Floating/Full Width, Auto-Hide, Taskbar, Startup),
    /// Apps & Pinning, Widgets Config, and General.
    /// </summary>
    public class SettingsWindow : Window
    {
        private readonly MonolithConfig _cfg;
        private readonly SystemTelemetry _tel;
        private readonly Action _onChanged;
        private readonly Action _onExitRestore;
        private readonly Action _onMediaLiveUpdate;
        private readonly double _dockAspect;
        private readonly Action _onOpenWidgetPicker;

        private ThemePalette _palette;
        private string _tab = "Appearance";
        private string _widgetCategory = "All";
        private StackPanel _navPanel;
        private ScrollViewer _contentScroll;
        private AnimatedBackdropHost _mediaPreviewHost;
        private MediaCropPicker _cropPicker;

        public SettingsWindow(MonolithConfig cfg, SystemTelemetry tel, Action onChanged, Action onExitRestore, Action onMediaLiveUpdate = null, double dockAspect = 9.5, Action onOpenWidgetPicker = null)
        {
            _cfg = cfg;
            _tel = tel;
            _onChanged = onChanged;
            _onExitRestore = onExitRestore;
            _onMediaLiveUpdate = onMediaLiveUpdate;
            _dockAspect = dockAspect > 2.0 ? dockAspect : 9.5;
            _onOpenWidgetPicker = onOpenWidgetPicker;
            _palette = ThemeManager.GetPalette(cfg);

            Title = "Monolith Settings";
            Width = 790;
            Height = 580;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            PreviewKeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) Close();
            };
            Closed += delegate
            {
                DisposeMediaPreviews();
            };

            RebuildAll();
        }

        private void ApplyAndRefresh(bool rebuildSelf)
        {
            ConfigStore.Save(_cfg);
            _palette = ThemeManager.GetPalette(_cfg);
            if (_onChanged != null) _onChanged();
            if (rebuildSelf) RebuildAll();
        }

        private void RebuildAll()
        {
            Border shell = new Border
            {
                CornerRadius = new CornerRadius(24),
                Background = ThemeManager.Brush(_palette.ModalBg),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(16),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 36,
                    ShadowDepth = 12,
                    Opacity = 0.58,
                    Color = Colors.Black
                }
            };

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Header
            Border hdr = new Border
            {
                BorderBrush = ThemeManager.Brush(_palette.DividerColor),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(20, 0, 16, 0),
                Background = Brushes.Transparent
            };
            DockPanel dp = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
            dp.Children.Add(new TextBlock
            {
                Text = "Monolith " + L10n.T(_cfg, "Ayarlar", "Settings"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            });

            TextBlock closeTxt = new TextBlock
            {
                Text = "\u2715",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(_palette.TextSecondary),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Border closeBtn = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = closeTxt
            };
            closeBtn.MouseEnter += delegate
            {
                closeBtn.Background = ThemeManager.Brush(Color.FromRgb(239, 68, 68));
                closeTxt.Foreground = Brushes.White;
            };
            closeBtn.MouseLeave += delegate
            {
                closeBtn.Background = ThemeManager.Brush(_palette.ModalSurface);
                closeTxt.Foreground = ThemeManager.Brush(_palette.TextSecondary);
            };
            closeBtn.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                Close();
            };
            DockPanel.SetDock(closeBtn, Dock.Right);
            dp.Children.Add(closeBtn);
            hdr.Child = dp;
            hdr.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (e.ButtonState == MouseButtonState.Pressed && !closeBtn.IsMouseOver)
                {
                    try { DragMove(); } catch { }
                }
            };
            Grid.SetRow(hdr, 0);
            root.Children.Add(hdr);

            // Body
            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border navBorder = new Border
            {
                BorderBrush = ThemeManager.Brush(_palette.DividerColor),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(12, 14, 12, 14)
            };
            _navPanel = new StackPanel();
            navBorder.Child = _navPanel;
            Grid.SetColumn(navBorder, 0);
            body.Children.Add(navBorder);

            _contentScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(20, 16, 20, 20)
            };
            _contentScroll.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e)
            {
                _contentScroll.ScrollToVerticalOffset(_contentScroll.VerticalOffset - e.Delta * 0.5);
                e.Handled = true;
            };
            Grid.SetColumn(_contentScroll, 1);
            body.Children.Add(_contentScroll);

            Grid.SetRow(body, 1);
            root.Children.Add(body);
            shell.Child = root;
            Content = shell;

            BuildNav();
            BuildTabContent();
        }

        private void BuildNav()
        {
            _navPanel.Children.Clear();
            string[,] tabs = new string[,]
            {
                { "Appearance",    L10n.T(_cfg, "Görünüm & Renk", "Appearance"),       "\uE790" },
                { "IconsMedia",    L10n.T(_cfg, "İkon & Medya", "Icons & Media"),      "\uE91B" },
                { "Dock",          L10n.T(_cfg, "Dock & Konum", "Dock"),               "\uE74C" },
                { "ControlCenter", L10n.T(_cfg, "Kontrol Merkezi", "Control Center"),  "\uE9E9" },
                { "Apps",          L10n.T(_cfg, "Sabit Uygulamalar", "Pinned Apps"),   "\uE71D" },
                { "Widgets",       L10n.T(_cfg, "Widget Ekle & Ayar", "Widgets"),      "\uE710" },
                { "General",       L10n.T(_cfg, "Genel", "General"),                   "\uE713" }
            };

            for (int i = 0; i < tabs.GetLength(0); i++)
            {
                string key = tabs[i, 0];
                string lbl = tabs[i, 1];
                string glyph = tabs[i, 2];
                bool active = (key == _tab);

                Border btn = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(11, 9, 11, 9),
                    Margin = new Thickness(0, 0, 0, 5),
                    Cursor = Cursors.Hand,
                    Background = active ? ThemeManager.Brush(_palette.Accent) : Brushes.Transparent
                };
                StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock
                {
                    Text = glyph,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 13,
                    Foreground = active ? Brushes.White : ThemeManager.Brush(_palette.TextSecondary),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 9, 0)
                });
                sp.Children.Add(new TextBlock
                {
                    Text = lbl,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 13,
                    FontWeight = active ? FontWeights.Bold : FontWeights.Medium,
                    Foreground = active ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary),
                    VerticalAlignment = VerticalAlignment.Center
                });
                btn.Child = sp;

                if (!active)
                {
                    btn.MouseEnter += delegate { btn.Background = ThemeManager.Brush(_palette.ModalSurface); };
                    btn.MouseLeave += delegate { btn.Background = Brushes.Transparent; };
                }
                btn.MouseLeftButtonUp += delegate
                {
                    _tab = key;
                    BuildNav();
                    BuildTabContent();
                };
                _navPanel.Children.Add(btn);
            }
        }

        private void DisposeMediaPreviews()
        {
            try { if (_mediaPreviewHost != null) { _mediaPreviewHost.ClearCurrent(); _mediaPreviewHost = null; } } catch { }
            try { if (_cropPicker != null) { _cropPicker.DisposeMedia(); _cropPicker = null; } } catch { }
        }

        private void BuildTabContent()
        {
            DisposeMediaPreviews();
            if (_tab == "Appearance") _contentScroll.Content = BuildAppearanceTab();
            else if (_tab == "IconsMedia") _contentScroll.Content = BuildIconsMediaTab();
            else if (_tab == "Dock") _contentScroll.Content = BuildDockTab();
            else if (_tab == "ControlCenter") _contentScroll.Content = BuildControlCenterTab();
            else if (_tab == "Apps") _contentScroll.Content = BuildAppsTab();
            else if (_tab == "Widgets") _contentScroll.Content = BuildWidgetsTab();
            else _contentScroll.Content = BuildGeneralTab();
        }

        private TextBlock SectionTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                Margin = new Thickness(0, 4, 0, 10)
            };
        }

        // ====== TAB 1: APPEARANCE ======
        private UIElement BuildAppearanceTab()
        {
            StackPanel sp = new StackPanel();
            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Dock Stili (Tema)", "Dock Style")));

            WrapPanel themeGrid = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
            for (int i = 0; i < ThemeManager.ThemeNames.Length; i++)
            {
                string tName = ThemeManager.ThemeNames[i];
                bool selected = (_cfg.Theme == tName);

                Border card = new Border
                {
                    Width = 154,
                    Height = 68,
                    CornerRadius = new CornerRadius(13),
                    Margin = new Thickness(0, 0, 10, 10),
                    Padding = new Thickness(10),
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(selected ? 2 : 1),
                    BorderBrush = ThemeManager.Brush(selected ? _palette.Accent : _palette.ModalBorder),
                    Background = BuildThemeSwatchBg(tName)
                };
                StackPanel inner = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };
                bool lightSwatch = (tName == "Ceramic White");
                inner.Children.Add(new TextBlock
                {
                    Text = tName,
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = lightSwatch ? ThemeManager.Brush(Color.FromRgb(18, 22, 30)) : Brushes.White
                });
                inner.Children.Add(new TextBlock
                {
                    Text = tName == "Spatial Glass" ? L10n.T(_cfg, "Kristal derinlik camı", "Crystalline depth glass")
                         : tName == "Prism Glass" ? L10n.T(_cfg, "Canlı optik kırılma", "Live optical refraction")
                         : tName == "Glacier Frost" ? L10n.T(_cfg, "Buzlu mavi cam", "Cool frosted glass")
                         : tName == "Aurora Glass" ? L10n.T(_cfg, "Kuzey ışığı camı", "Iridescent aurora glass")
                         : tName == "Obsidian Neon" ? L10n.T(_cfg, "Neon kenarlı karbon", "Neon accent border")
                         : tName == "Clear" ? L10n.T(_cfg, "Şeffaf çerçeve", "Transparent frame")
                         : selected ? L10n.T(_cfg, "Seçili", "Active") : "",
                    FontSize = 10,
                    Foreground = lightSwatch ? ThemeManager.Brush(Color.FromRgb(80, 92, 110)) : ThemeManager.Brush(Color.FromArgb(205, 255, 255, 255))
                });
                card.Child = inner;
                card.MouseLeftButtonUp += delegate
                {
                    _cfg.Theme = tName;
                    ApplyAndRefresh(true);
                };
                themeGrid.Children.Add(card);
            }
            sp.Children.Add(themeGrid);

            // Widgets on Clear selector when Clear is active
            if (_cfg.Theme == "Clear")
            {
                sp.Children.Add(SectionTitle(L10n.T(_cfg, "Clear Modunda Kart Stili", "Widgets on Clear")));
                sp.Children.Add(MakeSegmented(new string[] { "Glass", "Black", "White" }, _cfg.ClearCardStyle, delegate(string v)
                {
                    _cfg.ClearCardStyle = v;
                    ApplyAndRefresh(true);
                }));
            }

            // Dock Glass Color Tint (12 curated jewel tones + Natural + Auto-Sync with Accent!)
            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Dock Cam Renk Tonu", "Dock Glass Color Tint")));
            WrapPanel tintWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };

            // Natural & Sync-with-Accent pills
            tintWrap.Children.Add(MakeTintChip("Natural", L10n.T(_cfg, "Doğal", "Natural"), Color.FromRgb(148, 163, 184)));
            tintWrap.Children.Add(MakeTintChip("Accent", L10n.T(_cfg, "Vurguyla Eşle", "Match Accent"), _palette.Accent));
            foreach (System.Collections.Generic.KeyValuePair<string, Color> tk in ThemeManager.DockTints)
            {
                tintWrap.Children.Add(MakeTintChip(tk.Key, GetTintLabel(tk.Key), tk.Value));
            }
            sp.Children.Add(tintWrap);

            if (_cfg.DockTintColor != "Natural")
            {
                sp.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, "Renk Tonu Yoğunluğu", "Color Tint Intensity") + " (" + (int)Math.Round(_cfg.ColorTintStrength * 100) + "%)",
                    FontSize = 11.5,
                    Foreground = ThemeManager.Brush(_palette.TextSecondary),
                    Margin = new Thickness(0, 0, 0, 4)
                });
                Slider tSlider = new Slider
                {
                    Minimum = 0.10,
                    Maximum = 0.90,
                    Value = _cfg.ColorTintStrength,
                    Width = 360,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 12)
                };
                tSlider.ValueChanged += delegate
                {
                    _cfg.ColorTintStrength = Math.Round(tSlider.Value, 2);
                    ApplyAndRefresh(false);
                };
                sp.Children.Add(tSlider);
            }

            // 18 Accent colors + Custom HEX picker!
            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Vurgu Rengi (18 Renk + Özel HEX)", "Accent Colour (18 Swatches + Custom HEX)")));
            WrapPanel accPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            foreach (System.Collections.Generic.KeyValuePair<string, Color> kv in ThemeManager.Accents)
            {
                string aName = kv.Key;
                Color aCol = kv.Value;
                bool sel = (_cfg.Accent == aName);
                Border ring = new Border
                {
                    Width = 30,
                    Height = 30,
                    CornerRadius = new CornerRadius(15),
                    Margin = new Thickness(0, 0, 8, 8),
                    Padding = new Thickness(2.5),
                    BorderBrush = sel ? ThemeManager.Brush(_palette.TextPrimary) : Brushes.Transparent,
                    BorderThickness = new Thickness(2),
                    Cursor = Cursors.Hand,
                    ToolTip = aName
                };
                ring.Child = new Border
                {
                    CornerRadius = new CornerRadius(13),
                    Background = ThemeManager.Brush(aCol)
                };
                ring.MouseLeftButtonUp += delegate
                {
                    _cfg.Accent = aName;
                    ApplyAndRefresh(true);
                };
                accPanel.Children.Add(ring);
            }
            sp.Children.Add(accPanel);

            // Custom HEX color row
            StackPanel hexRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16), VerticalAlignment = VerticalAlignment.Center };
            Border hexPreview = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = ThemeManager.Brush(_palette.Accent),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            TextBox hexInput = new TextBox
            {
                Text = string.IsNullOrEmpty(_cfg.CustomAccentHex) ? string.Format("#{0:X2}{1:X2}{2:X2}", _palette.Accent.R, _palette.Accent.G, _palette.Accent.B) : _cfg.CustomAccentHex,
                Width = 104,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12.5,
                Padding = new Thickness(8, 5, 8, 5),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                CaretBrush = ThemeManager.Brush(_palette.TextPrimary),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            hexInput.TextChanged += delegate
            {
                Color parsed;
                if (ThemeManager.TryParseHexColor(hexInput.Text, out parsed))
                    hexPreview.Background = ThemeManager.Brush(parsed);
            };
            Button applyHex = MakePrimaryBtn(L10n.T(_cfg, "Vurgu Yap", "Set Accent"), delegate
            {
                Color parsed;
                if (ThemeManager.TryParseHexColor(hexInput.Text, out parsed))
                {
                    string norm = string.Format("#{0:X2}{1:X2}{2:X2}", parsed.R, parsed.G, parsed.B);
                    _cfg.CustomAccentHex = norm;
                    _cfg.Accent = "Custom";
                    ApplyAndRefresh(true);
                }
            });
            applyHex.Margin = new Thickness(8, 0, 0, 0);
            applyHex.Padding = new Thickness(11, 5, 11, 5);

            Button applyTintHex = MakePrimaryBtn(L10n.T(_cfg, "Dock Rengi Yap", "Set Dock Tint"), delegate
            {
                Color parsed;
                if (ThemeManager.TryParseHexColor(hexInput.Text, out parsed))
                {
                    string norm = string.Format("#{0:X2}{1:X2}{2:X2}", parsed.R, parsed.G, parsed.B);
                    _cfg.DockTintColor = norm;
                    ApplyAndRefresh(true);
                }
            });
            applyTintHex.Margin = new Thickness(6, 0, 0, 0);
            applyTintHex.Padding = new Thickness(11, 5, 11, 5);

            hexRow.Children.Add(hexPreview);
            hexRow.Children.Add(hexInput);
            hexRow.Children.Add(applyHex);
            hexRow.Children.Add(applyTintHex);
            sp.Children.Add(hexRow);

            // Glass Refraction Intensity
            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Spatial / Cam Kırılma Yoğunluğu", "Spatial / Glass Refraction Intensity")));
            Slider gSlider = new Slider
            {
                Minimum = 0.25,
                Maximum = 1.0,
                Value = _cfg.GlassIntensity,
                Width = 360,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            gSlider.ValueChanged += delegate
            {
                _cfg.GlassIntensity = Math.Round(gSlider.Value, 2);
                ApplyAndRefresh(false);
            };
            sp.Children.Add(gSlider);

            return sp;
        }

        private string GetTintLabel(string key)
        {
            if (key == "Sapphire") return L10n.T(_cfg, "Safir", "Sapphire");
            if (key == "Royal") return L10n.T(_cfg, "Mor", "Royal");
            if (key == "Magenta") return L10n.T(_cfg, "Fuşya", "Magenta");
            if (key == "Rose") return L10n.T(_cfg, "Gül", "Rose");
            if (key == "Crimson") return L10n.T(_cfg, "Yakut", "Crimson");
            if (key == "Sunset") return L10n.T(_cfg, "Turuncu", "Sunset");
            if (key == "Amber") return L10n.T(_cfg, "Altın", "Amber");
            if (key == "Emerald") return L10n.T(_cfg, "Zümrüt", "Emerald");
            if (key == "Ocean") return L10n.T(_cfg, "Okyanus", "Ocean");
            if (key == "Slate") return L10n.T(_cfg, "Kömür", "Slate");
            return key;
        }

        private Border MakeTintChip(string key, string label, Color swatchCol)
        {
            bool sel = (_cfg.DockTintColor == key);
            Border pill = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = sel
                    ? ThemeManager.Brush(Color.FromArgb(62, swatchCol.R, swatchCol.G, swatchCol.B))
                    : ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(sel ? _palette.TextPrimary : _palette.ModalBorder),
                BorderThickness = new Thickness(sel ? 1.6 : 1.0),
                Padding = new Thickness(9, 5, 11, 5),
                Margin = new Thickness(0, 0, 7, 7),
                Cursor = Cursors.Hand
            };
            StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                Background = ThemeManager.Brush(swatchCol),
                BorderBrush = ThemeManager.Brush(Color.FromArgb(140, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.5,
                FontWeight = sel ? FontWeights.Bold : FontWeights.Medium,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            });
            pill.Child = sp;
            pill.MouseLeftButtonUp += delegate
            {
                _cfg.DockTintColor = key;
                ApplyAndRefresh(true);
            };
            return pill;
        }

        private Brush BuildThemeSwatchBg(string tName)
        {
            if (tName == "Spatial Glass")
            {
                LinearGradientBrush lg = new LinearGradientBrush();
                lg.StartPoint = new Point(0, 0);
                lg.EndPoint = new Point(1, 1);
                lg.GradientStops.Add(new GradientStop(Color.FromRgb(118, 152, 204), 0));
                lg.GradientStops.Add(new GradientStop(Color.FromRgb(68, 92, 146), 0.5));
                lg.GradientStops.Add(new GradientStop(Color.FromRgb(32, 46, 82), 1));
                lg.Freeze();
                return lg;
            }
            if (tName == "Prism Glass")
            {
                LinearGradientBrush lg = new LinearGradientBrush();
                lg.StartPoint = new Point(0, 0);
                lg.EndPoint = new Point(1, 1);
                lg.GradientStops.Add(new GradientStop(Color.FromRgb(40, 88, 130), 0));
                lg.GradientStops.Add(new GradientStop(Color.FromRgb(180, 92, 62), 0.55));
                lg.GradientStops.Add(new GradientStop(Color.FromRgb(42, 48, 72), 1));
                lg.Freeze();
                return lg;
            }
            if (tName == "Aurora Glass")
            {
                LinearGradientBrush lg = new LinearGradientBrush();
                lg.StartPoint = new Point(0, 0);
                lg.EndPoint = new Point(1, 1);
                lg.GradientStops.Add(new GradientStop(Color.FromRgb(44, 28, 92), 0));
                lg.GradientStops.Add(new GradientStop(Color.FromRgb(118, 56, 152), 0.5));
                lg.GradientStops.Add(new GradientStop(Color.FromRgb(28, 92, 134), 1));
                lg.Freeze();
                return lg;
            }
            if (tName == "Obsidian Neon")
            {
                LinearGradientBrush lg = new LinearGradientBrush(Color.FromRgb(14, 17, 26), Color.FromRgb(22, 26, 40), 45);
                lg.Freeze();
                return lg;
            }
            if (tName == "Glacier Frost")
            {
                LinearGradientBrush lg = new LinearGradientBrush(Color.FromRgb(56, 86, 130), Color.FromRgb(30, 48, 78), 45);
                lg.Freeze();
                return lg;
            }
            if (tName == "Midnight") return ThemeManager.Brush(Color.FromRgb(16, 24, 48));
            if (tName == "Ceramic White") return ThemeManager.Brush(Color.FromRgb(245, 247, 250));
            return ThemeManager.Brush(Color.FromArgb(45, 148, 163, 184));
        }

        // ====== TAB 1B: 18 ICON STYLES + ANIMATED DOCK BACKGROUND MEDIA ======
        private UIElement BuildIconsMediaTab()
        {
            StackPanel sp = new StackPanel();

            // Section 1: Animated / Static Dock Background Media (GIF / MP4 / Image / Live Presets)
            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Hareketli Dock Arka Planı (GIF / Video / Görsel)", "Animated Dock Background (GIF / Video / Image)")));

            WrapPanel mediaPresets = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            mediaPresets.Children.Add(MakeMediaChip("None", L10n.T(_cfg, "Kapalı", "Off"), Color.FromRgb(100, 116, 139)));
            mediaPresets.Children.Add(MakeMediaChip("PresetAurora", L10n.T(_cfg, "Aurora Akışı", "Aurora Flow"), Color.FromRgb(139, 92, 246)));
            mediaPresets.Children.Add(MakeMediaChip("PresetCyber", L10n.T(_cfg, "Siber Neon", "Cyber Neon"), Color.FromRgb(217, 70, 239)));
            mediaPresets.Children.Add(MakeMediaChip("PresetSunset", L10n.T(_cfg, "Lav Dalgası", "Sunset Lava"), Color.FromRgb(249, 115, 22)));
            sp.Children.Add(mediaPresets);

            // Upload custom GIF / MP4 / Image row
            StackPanel uploadRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6), VerticalAlignment = VerticalAlignment.Center };
            Border pickMedia = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = ThemeManager.Brush(_palette.Accent),
                Padding = new Thickness(13, 8, 14, 8),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            StackPanel pickSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            pickSp.Children.Add(new TextBlock
            {
                Text = "\uE8B9",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 13.5,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            pickSp.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, "GIF / Video / Görsel Yükle...", "Upload GIF / Video / Image..."),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.2,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            });
            pickMedia.Child = pickSp;
            pickMedia.MouseEnter += delegate { pickMedia.Opacity = 0.88; };
            pickMedia.MouseLeave += delegate { pickMedia.Opacity = 1.0; };
            pickMedia.MouseLeftButtonUp += delegate
            {
                OpenFileDialog ofd = new OpenFileDialog
                {
                    Filter = "Hareketli & Görsel (*.gif;*.mp4;*.wmv;*.mov;*.avi;*.png;*.jpg;*.jpeg;*.webp;*.jfif;*.bmp)|*.gif;*.mp4;*.wmv;*.mov;*.avi;*.png;*.jpg;*.jpeg;*.webp;*.jfif;*.bmp|GIF (*.gif)|*.gif|Video (*.mp4;*.wmv;*.mov;*.avi)|*.mp4;*.wmv;*.mov;*.avi|Görsel (*.png;*.jpg;*.jpeg;*.webp;*.bmp)|*.png;*.jpg;*.jpeg;*.webp;*.bmp",
                    Title = L10n.T(_cfg, "Dock Arka Planı İçin GIF, Video veya Görsel Seç", "Choose GIF, Video or Image for Dock Background")
                };
                bool prevTop = Topmost;
                Topmost = false;
                bool? ok = ofd.ShowDialog(this);
                Topmost = prevTop;
                Activate();
                if (ok == true)
                {
                    _cfg.DockMediaPath = ofd.FileName;
                    _cfg.DockMediaMode = "CustomFile";
                    _cfg.DockMediaFit = "Crop";
                    _cfg.DockMediaOffsetX = 0.5;
                    _cfg.DockMediaOffsetY = 0.5;
                    _cfg.DockMediaZoom = 1.0;
                    if (_cfg.DockMediaOpacity < 0.70) _cfg.DockMediaOpacity = 0.85;
                    ApplyAndRefresh(false);
                    BuildTabContent();
                }
            };
            uploadRow.Children.Add(pickMedia);

            if (_cfg.DockMediaMode == "CustomFile" && !string.IsNullOrEmpty(_cfg.DockMediaPath))
            {
                Border filePill = new Border
                {
                    CornerRadius = new CornerRadius(9),
                    Background = ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = ThemeManager.Brush(_palette.Accent),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(8, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                filePill.Child = new TextBlock
                {
                    Text = Path.GetFileName(_cfg.DockMediaPath),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    MaxWidth = 170,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                uploadRow.Children.Add(filePill);

                Border clearBtn = MakeSmallBtn("\u2715", delegate
                {
                    _cfg.DockMediaMode = "None";
                    _cfg.DockMediaPath = "";
                    ApplyAndRefresh(false);
                    BuildTabContent();
                });
                uploadRow.Children.Add(clearBtn);
            }
            sp.Children.Add(uploadRow);

            sp.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, "GIF, MP4, WMV, MOV, PNG, JPG veya WEBP yükleyebilirsiniz. Tam ekran oyunlarda otomatik duraklatılır.", "Supports GIF, MP4, WMV, MOV, PNG, JPG or WEBP. Auto-pauses in fullscreen games."),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Foreground = ThemeManager.Brush(_palette.TextMuted),
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });

            // Live Region Cropper + Dock Media Preview + Fit mode + Opacity slider
            if (_cfg.DockMediaMode != "None")
            {
                // Mode selector (zero squishing: Crop Region / Tile Repeat / Contain Center)
                if (_cfg.DockMediaMode == "CustomFile")
                {
                    string curFitLbl = (_cfg.DockMediaFit == "Tile")
                        ? L10n.T(_cfg, "Döşe (Tekrarla)", "Tile Repeat")
                        : (_cfg.DockMediaFit == "Contain" ? L10n.T(_cfg, "Ortala", "Center") : L10n.T(_cfg, "Bölge Seç (Önerilen)", "Pick Region"));
                    sp.Children.Add(MakeSegmented(
                        new string[] { L10n.T(_cfg, "Bölge Seç (Önerilen)", "Pick Region"), L10n.T(_cfg, "Döşe (Tekrarla)", "Tile Repeat"), L10n.T(_cfg, "Ortala", "Center") },
                        curFitLbl,
                        delegate(string v)
                        {
                            if (v == "Döşe (Tekrarla)" || v == "Tile Repeat") _cfg.DockMediaFit = "Tile";
                            else if (v == "Ortala" || v == "Center") _cfg.DockMediaFit = "Contain";
                            else _cfg.DockMediaFit = "Crop";
                            ApplyAndRefresh(false);
                            BuildTabContent();
                        }));
                }

                // Live Interactive Region Picker when CustomFile + Crop mode!
                Slider zoomSlider = null;
                TextBlock zoomLbl = null;
                bool updatingZoomUi = false;

                if (_cfg.DockMediaMode == "CustomFile" && _cfg.DockMediaFit == "Crop" && !string.IsNullOrEmpty(_cfg.DockMediaPath))
                {
                    sp.Children.Add(new TextBlock
                    {
                        Text = L10n.T(_cfg, "Oynatılacak Bölgeyi Seç (Mavi çerçeveyi görsel/GIF üzerinde sürükleyin)", "Select Playable Region (Drag the blue window across your image/GIF)"),
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 11.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = ThemeManager.Brush(_palette.TextSecondary),
                        Margin = new Thickness(0, 0, 0, 6)
                    });

                    _cropPicker = new MediaCropPicker(_cfg, _palette, _dockAspect);
                    _cropPicker.Margin = new Thickness(0, 0, 0, 8);
                    sp.Children.Add(_cropPicker);

                    // Zoom slider + Reset button row
                    StackPanel zoomRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
                    zoomLbl = new TextBlock
                    {
                        Text = L10n.T(_cfg, "Yakınlaştır: ", "Zoom: ") + _cfg.DockMediaZoom.ToString("0.0") + "x",
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 11.5,
                        Foreground = ThemeManager.Brush(_palette.TextSecondary),
                        Width = 104,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    zoomRow.Children.Add(zoomLbl);

                    zoomSlider = new Slider
                    {
                        Minimum = 1.0,
                        Maximum = 3.5,
                        Value = _cfg.DockMediaZoom,
                        Width = 248,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    zoomRow.Children.Add(zoomSlider);

                    Border resetCropBtn = new Border
                    {
                        CornerRadius = new CornerRadius(8),
                        Background = ThemeManager.Brush(_palette.ModalSurface),
                        BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(10, 4, 10, 4),
                        Cursor = Cursors.Hand,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    resetCropBtn.Child = new TextBlock
                    {
                        Text = L10n.T(_cfg, "Sıfırla", "Reset"),
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = ThemeManager.Brush(_palette.TextPrimary)
                    };
                    resetCropBtn.MouseLeftButtonUp += delegate
                    {
                        _cfg.DockMediaOffsetX = 0.5;
                        _cfg.DockMediaOffsetY = 0.5;
                        _cfg.DockMediaZoom = 1.0;
                        updatingZoomUi = true;
                        zoomSlider.Value = 1.0;
                        zoomLbl.Text = L10n.T(_cfg, "Yakınlaştır: ", "Zoom: ") + "1.0x";
                        updatingZoomUi = false;
                        if (_cropPicker != null) _cropPicker.SetRegionExternal(0.5, 0.5, 1.0);
                        if (_mediaPreviewHost != null) _mediaPreviewHost.UpdateRegion(0.5, 0.5, 1.0);
                        ConfigStore.Save(_cfg);
                        if (_onMediaLiveUpdate != null) _onMediaLiveUpdate();
                        else ApplyAndRefresh(false);
                    };
                    zoomRow.Children.Add(resetCropBtn);
                    sp.Children.Add(zoomRow);

                    zoomSlider.ValueChanged += delegate
                    {
                        if (updatingZoomUi) return;
                        _cfg.DockMediaZoom = Math.Round(zoomSlider.Value, 2);
                        zoomLbl.Text = L10n.T(_cfg, "Yakınlaştır: ", "Zoom: ") + _cfg.DockMediaZoom.ToString("0.0") + "x";
                        if (_cropPicker != null) _cropPicker.SetRegionExternal(_cfg.DockMediaOffsetX, _cfg.DockMediaOffsetY, _cfg.DockMediaZoom);
                        if (_mediaPreviewHost != null) _mediaPreviewHost.UpdateRegion(_cfg.DockMediaOffsetX, _cfg.DockMediaOffsetY, _cfg.DockMediaZoom);
                        ConfigStore.Save(_cfg);
                        if (_onMediaLiveUpdate != null) _onMediaLiveUpdate();
                        else ApplyAndRefresh(false);
                    };

                    _cropPicker.RegionChanged += delegate(double ox, double oy, double zm, bool commit)
                    {
                        _cfg.DockMediaOffsetX = Math.Round(ox, 4);
                        _cfg.DockMediaOffsetY = Math.Round(oy, 4);
                        _cfg.DockMediaZoom = Math.Round(zm, 2);
                        if (zoomSlider != null && zoomLbl != null)
                        {
                            updatingZoomUi = true;
                            zoomSlider.Value = _cfg.DockMediaZoom;
                            zoomLbl.Text = L10n.T(_cfg, "Yakınlaştır: ", "Zoom: ") + _cfg.DockMediaZoom.ToString("0.0") + "x";
                            updatingZoomUi = false;
                        }
                        if (_mediaPreviewHost != null)
                            _mediaPreviewHost.UpdateRegion(_cfg.DockMediaOffsetX, _cfg.DockMediaOffsetY, _cfg.DockMediaZoom);
                        if (_onMediaLiveUpdate != null) _onMediaLiveUpdate();
                        if (commit)
                        {
                            ConfigStore.Save(_cfg);
                            if (_onMediaLiveUpdate == null) ApplyAndRefresh(false);
                        }
                    };
                }

                // Live Mini Dock Preview Capsule showing the EXACT result!
                sp.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, "Dock Önizlemesi", "Dock Preview"),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    Margin = new Thickness(0, 0, 0, 4)
                });

                Border prevCapsule = new Border
                {
                    Height = 52,
                    Width = 456,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(26),
                    Background = ThemeManager.Brush(_palette.DockTint),
                    BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                    BorderThickness = new Thickness(1.2),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                Grid prevGrid = new Grid();
                _mediaPreviewHost = new AnimatedBackdropHost();
                _mediaPreviewHost.SetClipRadius(26);
                _mediaPreviewHost.Configure(_cfg.DockMediaMode, _cfg.DockMediaPath, _cfg.DockMediaOpacity,
                    _cfg.DockMediaFit, _cfg.DockMediaOffsetX, _cfg.DockMediaOffsetY, _cfg.DockMediaZoom);
                prevGrid.Children.Add(_mediaPreviewHost);

                StackPanel prevIcons = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                string[] demoG = new string[] { "\uE80F", "\uE721", "\uE8B7", "\uE774", "\uE713" };
                for (int d = 0; d < demoG.Length; d++)
                {
                    Border mini = IconStyleFactory.CreateTile(_cfg.IconStyle, 32, _palette);
                    mini.IsHitTestVisible = false;
                    IconStyleFactory.SetContent(mini, new TextBlock
                    {
                        Text = demoG[d],
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 13,
                        Foreground = ThemeManager.Brush(_palette.TextPrimary),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    prevIcons.Children.Add(mini);
                }
                prevGrid.Children.Add(prevIcons);
                prevCapsule.Child = prevGrid;
                sp.Children.Add(prevCapsule);

                string errMsg = _mediaPreviewHost.LastError;
                if (string.IsNullOrEmpty(errMsg) && _cropPicker != null) errMsg = _cropPicker.LastError;
                if (!string.IsNullOrEmpty(errMsg))
                {
                    sp.Children.Add(new TextBlock
                    {
                        Text = errMsg,
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = ThemeManager.Brush(Color.FromRgb(248, 113, 113)),
                        Margin = new Thickness(0, 0, 0, 6),
                        TextWrapping = TextWrapping.Wrap
                    });
                }

                TextBlock opLabel = new TextBlock
                {
                    Text = L10n.T(_cfg, "Arka Plan Görsel Opaklığı", "Background Media Opacity") + " (" + (int)Math.Round(_cfg.DockMediaOpacity * 100) + "%)",
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.5,
                    Foreground = ThemeManager.Brush(_palette.TextSecondary),
                    Margin = new Thickness(0, 2, 0, 4)
                };
                sp.Children.Add(opLabel);
                Slider opSlider = new Slider
                {
                    Minimum = 0.15,
                    Maximum = 1.0,
                    Value = _cfg.DockMediaOpacity,
                    Width = 360,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 14)
                };
                opSlider.ValueChanged += delegate
                {
                    _cfg.DockMediaOpacity = Math.Round(opSlider.Value, 2);
                    opLabel.Text = L10n.T(_cfg, "Arka Plan Görsel Opaklığı", "Background Media Opacity") + " (" + (int)Math.Round(_cfg.DockMediaOpacity * 100) + "%)";
                    if (_mediaPreviewHost != null) _mediaPreviewHost.Opacity = _cfg.DockMediaOpacity;
                    ConfigStore.Save(_cfg);
                    if (_onMediaLiveUpdate != null) _onMediaLiveUpdate();
                    else ApplyAndRefresh(false);
                };
                sp.Children.Add(opSlider);
            }

            // Section 2: All 18 Live Selectable Icon Glass Styles!
            sp.Children.Add(SectionTitle(L10n.T(_cfg, "İkon Cam Stili (18 Seçenek)", "Icon Glass Style (18 Styles)")));

            WrapPanel styleGrid = new WrapPanel { Margin = new Thickness(0, 2, 0, 12) };
            for (int i = 0; i < IconStyleFactory.AllStyles.Count; i++)
            {
                IconStyleDef def = IconStyleFactory.AllStyles[i];
                bool sel = (_cfg.IconStyle == def.Id);

                Border card = new Border
                {
                    Width = 164,
                    CornerRadius = new CornerRadius(13),
                    Background = sel
                        ? ThemeManager.Brush(Color.FromArgb(46, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B))
                        : ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = ThemeManager.Brush(sel ? _palette.Accent : _palette.ModalBorder),
                    BorderThickness = new Thickness(sel ? 1.7 : 1.0),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 8, 8),
                    Cursor = Cursors.Hand
                };

                StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                // Render LIVE miniature preview of that exact icon style!
                Border miniOrb = IconStyleFactory.CreateTile(def.Id, 34, _palette);
                miniOrb.Margin = new Thickness(0, 0, 9, 0);
                miniOrb.IsHitTestVisible = false;
                IconStyleFactory.SetContent(miniOrb, new TextBlock
                {
                    Text = "\uE774",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 14,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Children.Add(miniOrb);

                StackPanel meta = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                meta.Children.Add(new TextBlock
                {
                    Text = (i + 1) + ". " + L10n.T(_cfg, def.NameTr, def.NameEn),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.5,
                    FontWeight = sel ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    MaxWidth = 104,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                if (sel)
                {
                    meta.Children.Add(new TextBlock
                    {
                        Text = L10n.T(_cfg, "Aktif", "Active"),
                        FontSize = 9.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = ThemeManager.Brush(_palette.Accent)
                    });
                }
                row.Children.Add(meta);

                card.Child = row;
                string capturedId = def.Id;
                card.MouseLeftButtonUp += delegate
                {
                    _cfg.IconStyle = capturedId;
                    ApplyAndRefresh(false);
                    BuildTabContent();
                };
                styleGrid.Children.Add(card);
            }
            sp.Children.Add(styleGrid);

            return sp;
        }

        private Border MakeMediaChip(string modeKey, string label, Color dotCol)
        {
            bool sel = (_cfg.DockMediaMode == modeKey);
            Border pill = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = sel
                    ? ThemeManager.Brush(Color.FromArgb(62, dotCol.R, dotCol.G, dotCol.B))
                    : ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(sel ? _palette.TextPrimary : _palette.ModalBorder),
                BorderThickness = new Thickness(sel ? 1.6 : 1.0),
                Padding = new Thickness(10, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 6),
                Cursor = Cursors.Hand
            };
            StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Background = ThemeManager.Brush(dotCol),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.8,
                FontWeight = sel ? FontWeights.Bold : FontWeights.Medium,
                Foreground = ThemeManager.Brush(_palette.TextPrimary)
            });
            pill.Child = sp;
            pill.MouseLeftButtonUp += delegate
            {
                _cfg.DockMediaMode = modeKey;
                ApplyAndRefresh(false);
                BuildTabContent();
            };
            return pill;
        }

        // ====== TAB 2: DOCK ======
        private UIElement BuildDockTab()
        {
            StackPanel sp = new StackPanel();
            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Dock Boyutu", "Dock Size")));
            sp.Children.Add(MakeSegmented(new string[] { "S", "M", "L" }, _cfg.DockSize, delegate(string v)
            {
                _cfg.DockSize = v;
                ApplyAndRefresh(false);
            }));

            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Ekran Konumu", "Position")));
            sp.Children.Add(MakeSegmented(
                new string[] { L10n.T(_cfg, "Alt Kenar", "Bottom"), L10n.T(_cfg, "Üst Kenar", "Top") },
                _cfg.IsTop ? L10n.T(_cfg, "Üst Kenar", "Top") : L10n.T(_cfg, "Alt Kenar", "Bottom"),
                delegate(string v)
                {
                    _cfg.IsTop = (v == "Top" || v == "Üst Kenar");
                    ApplyAndRefresh(false);
                }));

            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Pencere Çakışma Modu (Yüzen Dock)", "Window Overlap Behavior (Floating Dock)")));
            string curOvLbl = (_cfg.DockOverlapMode == "ReserveSpace")
                ? L10n.T(_cfg, "Alan Ayır (Engellemez)", "Reserve Space")
                : (_cfg.DockOverlapMode == "FloatOver" ? L10n.T(_cfg, "Üstte Yüz", "Float Over") : L10n.T(_cfg, "Akıllı Gizle (Önerilen)", "Smart Dodge"));
            sp.Children.Add(MakeSegmented(
                new string[] { L10n.T(_cfg, "Akıllı Gizle (Önerilen)", "Smart Dodge"), L10n.T(_cfg, "Alan Ayır (Engellemez)", "Reserve Space"), L10n.T(_cfg, "Üstte Yüz", "Float Over") },
                curOvLbl,
                delegate(string v)
                {
                    if (v == "Alan Ayır (Engellemez)" || v == "Reserve Space") _cfg.DockOverlapMode = "ReserveSpace";
                    else if (v == "Üstte Yüz" || v == "Float Over") _cfg.DockOverlapMode = "FloatOver";
                    else _cfg.DockOverlapMode = "SmartHide";
                    ApplyAndRefresh(false);
                }));
            sp.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg,
                    "Akıllı Gizle: Masaüstünde görünür, tam pencere altını kapatırken çekilir (fare alt kenara inince açılır). Alan Ayır: Pencereler tam dock'un üstünde biter.",
                    "Smart Dodge: Visible on desktop, tucks away when a window overlaps the dock (peeks at screen edge). Reserve Space: Maximized windows stop cleanly above the dock."),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10.8,
                Foreground = ThemeManager.Brush(_palette.TextMuted),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, -6, 0, 12)
            });

            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Arama Düğmesi Davranışı", "Search Button Mode")));
            sp.Children.Add(MakeSegmented(
                new string[] { L10n.T(_cfg, "Windows 11 Arama (Entegre)", "Windows 11 Search (Integrated)"), L10n.T(_cfg, "Klasik Win+S", "Classic Win+S") },
                (_cfg.SearchMode == "WindowsNative") ? L10n.T(_cfg, "Klasik Win+S", "Classic Win+S") : L10n.T(_cfg, "Windows 11 Arama (Entegre)", "Windows 11 Search (Integrated)"),
                delegate(string v)
                {
                    _cfg.SearchMode = (v == "Klasik Win+S" || v == "Classic Win+S") ? "WindowsNative" : "Integrated";
                    ApplyAndRefresh(false);
                }));

            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Davranış & Windows Görev Çubuğu", "Behavior & Taskbar")));

            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Dock Dalga Büyütme Efekti", "Wave Icon Magnification"), _cfg.DockMagnification, delegate(bool v) { _cfg.DockMagnification = v; ApplyAndRefresh(false); }));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Canlı Pencere Önizlemesi (DWM Hover)", "Live Window Previews on Hover (DWM)"), _cfg.LiveWindowPreviews, delegate(bool v) { _cfg.LiveWindowPreviews = v; ApplyAndRefresh(false); }));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Arama Düğmesini Göster (Büyüteç)", "Show Search Button"), _cfg.ShowSpotlightButton, delegate(bool v) { _cfg.ShowSpotlightButton = v; ApplyAndRefresh(false); }));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Kontrol Merkezi Düğmesini Göster", "Show Control Center Button"), _cfg.ShowControlCenterButton, delegate(bool v) { _cfg.ShowControlCenterButton = v; ApplyAndRefresh(false); }));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Tam Genişlik (Edge-to-Edge)", "Full Width Bar"), _cfg.FullWidth, delegate(bool v) { _cfg.FullWidth = v; ApplyAndRefresh(false); BuildTabContent(); }));
            if (_cfg.FullWidth)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, "Tam Genişlik İkon Hizalaması", "Full Width Icon Alignment"),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.5,
                    Foreground = ThemeManager.Brush(_palette.TextSecondary),
                    Margin = new Thickness(4, 2, 0, 5)
                });
                sp.Children.Add(MakeSegmented(
                    new string[] { L10n.T(_cfg, "Ortada (Windows 11)", "Centered (Windows 11)"), L10n.T(_cfg, "Solda (Klasik)", "Left (Classic)") },
                    (_cfg.FullWidthAlign == "Left") ? L10n.T(_cfg, "Solda (Klasik)", "Left (Classic)") : L10n.T(_cfg, "Ortada (Windows 11)", "Centered (Windows 11)"),
                    delegate(string v)
                    {
                        _cfg.FullWidthAlign = (v == "Solda (Klasik)" || v == "Left (Classic)") ? "Left" : "Center";
                        ApplyAndRefresh(false);
                    }));
            }
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Otomatik Gizle (Kenara gelince aç)", "Auto-Hide Dock"), _cfg.AutoHide, delegate(bool v) { _cfg.AutoHide = v; ApplyAndRefresh(false); }));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Tam Ekran Uygulama/Oyunlarda Gizle", "Hide in Fullscreen Apps/Games"), _cfg.HideOnFullscreen, delegate(bool v) { _cfg.HideOnFullscreen = v; ApplyAndRefresh(false); }));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Anti-Cheat Oyun Koruması (Oyunlarda Uyku & Sıfır Müdahale)", "Anti-Cheat Safe Game Mode (Zero-Touch & Deep Sleep)"), _cfg.AntiCheatSafeMode, delegate(bool v)
            {
                _cfg.AntiCheatSafeMode = v;
                NativeWin32.AntiCheatSafeMode = v;
                ApplyAndRefresh(false);
            }));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Düşük RAM / İşlemci Tasarruf Modu", "Low RAM & CPU Saver Mode"), _cfg.LowResourceMode, delegate(bool v)
            {
                _cfg.LowResourceMode = v;
                if (v) { _cfg.DockMagnification = false; _cfg.LiveWindowPreviews = false; NativeWin32.TrimWorkingSet(); }
                ApplyAndRefresh(false);
                BuildTabContent();
            }));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Üzerine Gelince İsim Göster", "Names on Hover"), _cfg.NamesOnHover, delegate(bool v) { _cfg.NamesOnHover = v; ApplyAndRefresh(false); }));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Açık Pencereleri Dock'ta Göster", "Show Open Windows"), _cfg.ShowOpenWindows, delegate(bool v) { _cfg.ShowOpenWindows = v; ApplyAndRefresh(false); }));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "24 Saat Formatı", "24-Hour Clock"), _cfg.Use24HourClock, delegate(bool v) { _cfg.Use24HourClock = v; ApplyAndRefresh(false); }));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Windows Görev Çubuğunu Gizle", "Hide Native Windows Taskbar"), _cfg.HideWindowsTaskbar, delegate(bool v)
            {
                _cfg.HideWindowsTaskbar = v;
                if (v) NativeWin32.HideNativeTaskbars();
                else NativeWin32.RestoreNativeTaskbars();
                ApplyAndRefresh(false);
            }));
            bool startupOn = NativeWin32.IsStartupEnabled();
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Windows Açılışında Başlat", "Launch at Windows Startup"), startupOn, delegate(bool v)
            {
                NativeWin32.SetStartupEnabled(v, _cfg.FastStartupMode);
                BuildTabContent();
            }));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Hızlı Açılış Modu (Yeniden Başlatmada Gecikmesiz Başlat)", "Instant Boot Startup (Zero Logon Delay via Task Scheduler)"), _cfg.FastStartupMode, delegate(bool v)
            {
                _cfg.FastStartupMode = v;
                ConfigStore.Save(_cfg);
                if (NativeWin32.IsStartupEnabled())
                    NativeWin32.SetStartupEnabled(true, v);
            }));

            return sp;
        }

        // ====== TAB 2B: CONTROL CENTER CUSTOMIZATION ======
        private UIElement BuildControlCenterTab()
        {
            StackPanel sp = new StackPanel();

            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Kontrol Merkezi Tasarım Şekli", "Control Center Layout Style")));
            string curStyleLbl = (_cfg.CcStyle == "Compact")
                ? L10n.T(_cfg, "Kompakt", "Compact")
                : (_cfg.CcStyle == "GlassCards" ? L10n.T(_cfg, "Cam Kartlar", "Glass Cards") : "Windows 11");
            sp.Children.Add(MakeSegmented(
                new string[] { "Windows 11", L10n.T(_cfg, "Kompakt", "Compact"), L10n.T(_cfg, "Cam Kartlar", "Glass Cards") },
                curStyleLbl,
                delegate(string v)
                {
                    if (v == "Kompakt" || v == "Compact") { _cfg.CcStyle = "Compact"; _cfg.CcColumns = 3; }
                    else if (v == "Cam Kartlar" || v == "Glass Cards") { _cfg.CcStyle = "GlassCards"; _cfg.CcColumns = 2; }
                    else { _cfg.CcStyle = "Windows11"; _cfg.CcColumns = 3; }
                    ApplyAndRefresh(false);
                }));

            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Köşe Şekli & Genişlik", "Corner Shape & Size")));
            string curCornerLbl = (_cfg.CcCornerStyle == "Sharp")
                ? L10n.T(_cfg, "Keskin", "Sharp")
                : (_cfg.CcCornerStyle == "Pill" ? L10n.T(_cfg, "Kapsül", "Pill") : L10n.T(_cfg, "Yumuşak", "Rounded"));
            sp.Children.Add(MakeSegmented(
                new string[] { L10n.T(_cfg, "Yumuşak", "Rounded"), L10n.T(_cfg, "Keskin", "Sharp"), L10n.T(_cfg, "Kapsül", "Pill") },
                curCornerLbl,
                delegate(string v)
                {
                    if (v == "Keskin" || v == "Sharp") _cfg.CcCornerStyle = "Sharp";
                    else if (v == "Kapsül" || v == "Pill") _cfg.CcCornerStyle = "Pill";
                    else _cfg.CcCornerStyle = "Rounded";
                    ApplyAndRefresh(false);
                }));

            sp.Children.Add(MakeSegmented(new string[] { "S", "M", "L" }, _cfg.CcSize ?? "M", delegate(string v)
            {
                _cfg.CcSize = v;
                ApplyAndRefresh(false);
            }));

            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Bölümler", "Visible Sections")));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Ekran Parlaklığı Kaydırıcısı (Laptop)", "Display Brightness Slider"), _cfg.CcShowBrightness, delegate(bool v) { _cfg.CcShowBrightness = v; ApplyAndRefresh(false); }));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Ses Seviyesi & Sessize Al", "Master Volume & Mute"), _cfg.CcShowVolume, delegate(bool v) { _cfg.CcShowVolume = v; ApplyAndRefresh(false); }));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Medya Oynatıcı Kartı", "Now Playing Media Card"), _cfg.CcShowMedia, delegate(bool v) { _cfg.CcShowMedia = v; ApplyAndRefresh(false); }));
            sp.Children.Add(MakeToggleRow(L10n.T(_cfg, "Güç Düğmeleri (Kilitle / Uyku / Yeniden / Kapat)", "Power Row (Lock / Sleep / Restart / Power)"), _cfg.CcShowPowerRow, delegate(bool v) { _cfg.CcShowPowerRow = v; ApplyAndRefresh(false); }));

            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Hızlı İşlem Kutuları (12 Seçenek — Kontrol Merkezinde Düzenle ile sıralanabilir)", "Quick Action Tiles (12 Options — reorder via Edit inside Control Center)")));
            WrapPanel tilesWrap = new WrapPanel { Margin = new Thickness(0, 2, 0, 12) };
            for (int i = 0; i < ControlCenterWindow.AllTiles.Count; i++)
            {
                ControlCenterTileDef td = ControlCenterWindow.AllTiles[i];
                bool on = (_cfg.CcActiveTiles != null && _cfg.CcActiveTiles.Contains(td.Id));
                Border chip = new Border
                {
                    CornerRadius = new CornerRadius(11),
                    Background = on
                        ? ThemeManager.Brush(Color.FromArgb(52, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B))
                        : ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = ThemeManager.Brush(on ? _palette.Accent : _palette.ModalBorder),
                    BorderThickness = new Thickness(on ? 1.5 : 1.0),
                    Padding = new Thickness(10, 7, 12, 7),
                    Margin = new Thickness(0, 0, 8, 8),
                    Cursor = Cursors.Hand
                };
                StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(new TextBlock
                {
                    Text = td.Glyph,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 13,
                    Foreground = on ? ThemeManager.Brush(_palette.Accent) : ThemeManager.Brush(_palette.TextSecondary),
                    Margin = new Thickness(0, 0, 7, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, td.NameTr, td.NameEn),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.8,
                    FontWeight = on ? FontWeights.Bold : FontWeights.Medium,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    VerticalAlignment = VerticalAlignment.Center
                });
                chip.Child = row;
                string capId = td.Id;
                chip.MouseLeftButtonUp += delegate
                {
                    if (_cfg.CcActiveTiles == null) _cfg.CcActiveTiles = new System.Collections.Generic.List<string>();
                    if (_cfg.CcActiveTiles.Contains(capId)) _cfg.CcActiveTiles.Remove(capId);
                    else _cfg.CcActiveTiles.Add(capId);
                    ApplyAndRefresh(false);
                    BuildTabContent();
                };
                tilesWrap.Children.Add(chip);
            }
            sp.Children.Add(tilesWrap);

            return sp;
        }

        // ====== TAB 3: PINNED APPS ======
        private UIElement BuildAppsTab()
        {
            StackPanel sp = new StackPanel();
            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Sabitlenmis Uygulamalar", "Pinned Applications")));

            Button addBtn = MakePrimaryBtn("+ " + L10n.T(_cfg, "Uygulama Ekle (.exe / .lnk)", "Add Application (.exe / .lnk)"), delegate
            {
                OpenFileDialog ofd = new OpenFileDialog
                {
                    Filter = "Programs (*.exe;*.lnk)|*.exe;*.lnk|All Files (*.*)|*.*",
                    Title = L10n.T(_cfg, "Dock'a Sabitlenecek Uygulamayi Sec", "Choose App to Pin")
                };
                if (ofd.ShowDialog() == true)
                {
                    string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                    _cfg.PinnedApps.Add(new PinnedAppItem
                    {
                        Name = name,
                        Path = ofd.FileName,
                        IconPath = ofd.FileName,
                        Glyph = "\uE71D"
                    });
                    ApplyAndRefresh(false);
                    BuildTabContent();
                }
            });
            addBtn.Margin = new Thickness(0, 0, 0, 12);
            sp.Children.Add(addBtn);

            for (int i = 0; i < _cfg.PinnedApps.Count; i++)
            {
                int idx = i;
                PinnedAppItem app = _cfg.PinnedApps[i];
                Border row = new Border
                {
                    CornerRadius = new CornerRadius(11),
                    Background = ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 7)
                };
                DockPanel dp = new DockPanel();

                StackPanel btns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                if (idx > 0)
                {
                    btns.Children.Add(MakeSmallBtn("\u2191", delegate
                    {
                        PinnedAppItem tmp = _cfg.PinnedApps[idx - 1];
                        _cfg.PinnedApps[idx - 1] = _cfg.PinnedApps[idx];
                        _cfg.PinnedApps[idx] = tmp;
                        ApplyAndRefresh(false);
                        BuildTabContent();
                    }));
                }
                if (idx < _cfg.PinnedApps.Count - 1)
                {
                    btns.Children.Add(MakeSmallBtn("\u2193", delegate
                    {
                        PinnedAppItem tmp = _cfg.PinnedApps[idx + 1];
                        _cfg.PinnedApps[idx + 1] = _cfg.PinnedApps[idx];
                        _cfg.PinnedApps[idx] = tmp;
                        ApplyAndRefresh(false);
                        BuildTabContent();
                    }));
                }
                btns.Children.Add(MakeSmallBtn("\u2715", delegate
                {
                    _cfg.PinnedApps.RemoveAt(idx);
                    ApplyAndRefresh(false);
                    BuildTabContent();
                }));

                DockPanel.SetDock(btns, Dock.Right);
                dp.Children.Add(btns);

                StackPanel meta = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                meta.Children.Add(new TextBlock { Text = app.Name, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = ThemeManager.Brush(_palette.TextPrimary) });
                meta.Children.Add(new TextBlock { Text = app.Path, FontSize = 10.5, Foreground = ThemeManager.Brush(_palette.TextMuted), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 340 });
                dp.Children.Add(meta);
                row.Child = dp;
                sp.Children.Add(row);
            }

            return sp;
        }

        // ====== TAB 4: WIDGETS (ADD / REMOVE + LIVE DATA CONFIG) ======
        private UIElement BuildWidgetsTab()
        {
            StackPanel sp = new StackPanel();

            // Section 1: Add / Remove Widgets (moved cleanly from Dock '+' into Settings!)
            DockPanel hdr = new DockPanel { Margin = new Thickness(0, 2, 0, 10) };
            hdr.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, "Dock Widget'ları (Ekle / Kaldır)", "Dock Widgets (Add / Remove)"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            });

            if (_onOpenWidgetPicker != null)
            {
                Border galleryBtn = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Background = ThemeManager.Brush(_palette.Accent),
                    Padding = new Thickness(11, 6, 12, 6),
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                StackPanel gsp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                gsp.Children.Add(new TextBlock
                {
                    Text = "\uE710",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 11.5,
                    Foreground = Brushes.White,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                gsp.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, "Büyük Galeri Penceresi", "Open Full Gallery"),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                });
                galleryBtn.Child = gsp;
                galleryBtn.MouseEnter += delegate { galleryBtn.Opacity = 0.88; };
                galleryBtn.MouseLeave += delegate { galleryBtn.Opacity = 1.0; };
                galleryBtn.MouseLeftButtonUp += delegate
                {
                    _onOpenWidgetPicker();
                };
                DockPanel.SetDock(galleryBtn, Dock.Right);
                hdr.Children.Add(galleryBtn);
            }
            sp.Children.Add(hdr);

            // Category filter pills
            WrapPanel catRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            for (int c = 0; c < WidgetCatalog.Categories.Length; c++)
            {
                string catKey = WidgetCatalog.Categories[c];
                bool catSel = (_widgetCategory == catKey);
                Border catPill = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Background = catSel ? ThemeManager.Brush(_palette.Accent) : ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = ThemeManager.Brush(catSel ? _palette.Accent : _palette.ModalBorder),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 4.5, 10, 4.5),
                    Margin = new Thickness(0, 0, 6, 6),
                    Cursor = Cursors.Hand
                };
                catPill.Child = new TextBlock
                {
                    Text = WidgetCatalog.GetCategoryLabel(_cfg, catKey),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.2,
                    FontWeight = catSel ? FontWeights.Bold : FontWeights.Medium,
                    Foreground = catSel ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary)
                };
                catPill.MouseLeftButtonUp += delegate
                {
                    _widgetCategory = catKey;
                    BuildTabContent();
                };
                catRow.Children.Add(catPill);
            }
            sp.Children.Add(catRow);

            // 2-column 1-click Widget Add/Remove Grid
            WrapPanel wGrid = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
            for (int i = 0; i < WidgetCatalog.All.Count; i++)
            {
                WidgetDef wd = WidgetCatalog.All[i];
                if (_widgetCategory != "All" && wd.Category != _widgetCategory) continue;
                bool active = (_cfg.ActiveWidgets != null && _cfg.ActiveWidgets.Contains(wd.Id));

                Border card = new Border
                {
                    Width = 250,
                    CornerRadius = new CornerRadius(12),
                    Background = active
                        ? ThemeManager.Brush(Color.FromArgb(46, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B))
                        : ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = ThemeManager.Brush(active ? _palette.Accent : _palette.ModalBorder),
                    BorderThickness = new Thickness(active ? 1.5 : 1.0),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 8, 8),
                    Cursor = Cursors.Hand
                };

                Grid g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                Color badgeC = _palette.Accent;
                try { badgeC = (Color)ColorConverter.ConvertFromString(wd.BadgeColorHex); } catch { }
                Border icoBox = new Border
                {
                    Width = 30,
                    Height = 30,
                    CornerRadius = new CornerRadius(9),
                    Background = ThemeManager.Brush(Color.FromArgb(45, badgeC.R, badgeC.G, badgeC.B)),
                    Margin = new Thickness(0, 0, 9, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                bool isMdl2 = !string.IsNullOrEmpty(wd.IconGlyph) && wd.IconGlyph[0] >= '\uE000';
                icoBox.Child = new TextBlock
                {
                    Text = wd.IconGlyph,
                    FontFamily = new FontFamily(isMdl2 ? "Segoe MDL2 Assets" : "Segoe UI Symbol"),
                    FontSize = 13.5,
                    Foreground = ThemeManager.Brush(badgeC),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(icoBox, 0);
                g.Children.Add(icoBox);

                StackPanel meta = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
                meta.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, wd.NameTr, wd.NameEn),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.8,
                    FontWeight = active ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                meta.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, wd.DescTr, wd.DescEn),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 9.8,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                Grid.SetColumn(meta, 1);
                g.Children.Add(meta);

                Border actionPill = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(12),
                    Background = active ? ThemeManager.Brush(_palette.Accent) : ThemeManager.Brush(Color.FromArgb(35, 148, 163, 184)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                actionPill.Child = new TextBlock
                {
                    Text = active ? "\uE73E" : "\uE710",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 11,
                    Foreground = active ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(actionPill, 2);
                g.Children.Add(actionPill);

                card.Child = g;
                string capId = wd.Id;
                card.MouseLeftButtonUp += delegate
                {
                    if (_cfg.ActiveWidgets == null) _cfg.ActiveWidgets = new System.Collections.Generic.List<string>();
                    if (_cfg.ActiveWidgets.Contains(capId)) _cfg.ActiveWidgets.Remove(capId);
                    else _cfg.ActiveWidgets.Add(capId);
                    ApplyAndRefresh(false);
                    BuildTabContent();
                };
                wGrid.Children.Add(card);
            }
            sp.Children.Add(wGrid);

            // Section 2: Live Widget Data Sources
            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Canlı Widget Veri Kaynakları", "Live Widget Data Settings")));

            TextBox cityTb = MakeInputRow(sp, L10n.T(_cfg, "Hava Durumu Şehri (örn. Istanbul, Ankara, Izmir)", "Weather City"), _cfg.WeatherCity);
            TextBox cryptoTb = MakeInputRow(sp, L10n.T(_cfg, "Kripto / Döviz Sembolü (BTC, ETH, SOL, USDTRY, EURTRY)", "Crypto / FX Symbol"), _cfg.CryptoSymbol);
            TextBox ghTb = MakeInputRow(sp, L10n.T(_cfg, "GitHub Kullanıcı Adı", "GitHub Username"), _cfg.GitHubUser);
            TextBox pomTb = MakeInputRow(sp, L10n.T(_cfg, "Varsayılan Pomodoro Süresi (dakika)", "Default Pomodoro (minutes)"), _cfg.PomodoroMinutes.ToString());

            Button saveBtn = MakePrimaryBtn(L10n.T(_cfg, "Kaydet ve Canlı Verileri Yenile", "Save & Refresh Live Data"), delegate
            {
                _cfg.WeatherCity = string.IsNullOrEmpty(cityTb.Text.Trim()) ? "Istanbul" : cityTb.Text.Trim();
                _cfg.CryptoSymbol = string.IsNullOrEmpty(cryptoTb.Text.Trim()) ? "BTC" : cryptoTb.Text.Trim().ToUpperInvariant();
                _cfg.GitHubUser = string.IsNullOrEmpty(ghTb.Text.Trim()) ? "torvalds" : ghTb.Text.Trim();
                int mins;
                if (int.TryParse(pomTb.Text.Trim(), out mins) && mins >= 1 && mins <= 120)
                {
                    _cfg.PomodoroMinutes = mins;
                    if (!_tel.PomodoroRunning)
                    {
                        _tel.PomodoroTotalSeconds = mins * 60;
                        _tel.PomodoroSecondsLeft = mins * 60;
                    }
                }
                ConfigStore.Save(_cfg);
                _tel.RefreshOnlineAsync(_cfg, true);
                if (_onChanged != null) _onChanged();
            });
            saveBtn.Margin = new Thickness(0, 12, 0, 0);
            sp.Children.Add(saveBtn);
            return sp;
        }

        // ====== TAB 5: GENERAL ======
        private UIElement BuildGeneralTab()
        {
            StackPanel sp = new StackPanel();
            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Dil / Language", "Language")));
            sp.Children.Add(MakeSegmented(new string[] { "Türkçe", "English" }, L10n.IsTr(_cfg) ? "Türkçe" : "English", delegate(string v)
            {
                _cfg.Language = (v == "Türkçe") ? "tr" : "en";
                ApplyAndRefresh(true);
            }));

            sp.Children.Add(SectionTitle(L10n.T(_cfg, "Klavye Kısayolları", "Keyboard Shortcuts")));
            Border scBox = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 18)
            };
            scBox.Child = new TextBlock
            {
                Text = "Ctrl + Shift + Space \u2192  " + L10n.T(_cfg, "Hızlı Arama & Hesap Makinesi", "Quick Search & Calculator") + "\n" +
                       "Ctrl + Shift + W     \u2192  " + L10n.T(_cfg, "Widget Galerisini aç", "Open All Widgets Picker") + "\n" +
                       "Ctrl + Shift + S     \u2192  " + L10n.T(_cfg, "Ayarları aç", "Open Settings") + "\n" +
                       "Ctrl + Shift + F11   \u2192  " + L10n.T(_cfg, "Monolith'ı gizle / göster", "Toggle dock visibility"),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = ThemeManager.Brush(_palette.TextSecondary),
                LineHeight = 22
            };
            sp.Children.Add(scBox);

            Button exitBtn = new Button
            {
                Content = L10n.T(_cfg, "Windows Görev Çubuğunu Geri Yükle ve Çık", "Restore Windows Taskbar & Exit"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Background = ThemeManager.Brush(Color.FromRgb(220, 38, 38)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16, 10, 16, 10),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };
            exitBtn.Click += delegate
            {
                Close();
                if (_onExitRestore != null) _onExitRestore();
            };
            sp.Children.Add(exitBtn);

            return sp;
        }

        private UIElement MakeSegmented(string[] options, string current, Action<string> onSelect)
        {
            Border wrap = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 14)
            };
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < options.Length; i++)
            {
                string opt = options[i];
                bool sel = (opt == current);
                Border pill = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 6, 16, 6),
                    Cursor = Cursors.Hand,
                    Background = sel ? ThemeManager.Brush(_palette.Accent) : Brushes.Transparent
                };
                pill.Child = new TextBlock
                {
                    Text = opt,
                    FontSize = 12.5,
                    FontWeight = sel ? FontWeights.Bold : FontWeights.Medium,
                    Foreground = sel ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary)
                };
                pill.MouseLeftButtonUp += delegate
                {
                    onSelect(opt);
                    BuildTabContent();
                };
                row.Children.Add(pill);
            }
            wrap.Child = row;
            return wrap;
        }

        private UIElement MakeToggleRow(string label, bool value, Action<bool> onChanged)
        {
            Border row = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(13, 9, 13, 9),
                Margin = new Thickness(0, 0, 0, 7),
                Cursor = Cursors.Hand
            };
            DockPanel dp = new DockPanel();
            dp.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12.5,
                FontWeight = FontWeights.Medium,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            });

            // Modern switch pill
            Border swTrack = new Border
            {
                Width = 40,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = value ? ThemeManager.Brush(_palette.Accent) : ThemeManager.Brush(Color.FromArgb(70, 148, 163, 184)),
                Padding = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Border knob = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = Brushes.White,
                HorizontalAlignment = value ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };
            swTrack.Child = knob;
            DockPanel.SetDock(swTrack, Dock.Right);
            dp.Children.Add(swTrack);
            row.Child = dp;

            bool state = value;
            row.MouseLeftButtonUp += delegate
            {
                state = !state;
                swTrack.Background = state ? ThemeManager.Brush(_palette.Accent) : ThemeManager.Brush(Color.FromArgb(70, 148, 163, 184));
                knob.HorizontalAlignment = state ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                onChanged(state);
            };
            return row;
        }

        private TextBox MakeInputRow(StackPanel parent, string label, string initialValue)
        {
            parent.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                Foreground = ThemeManager.Brush(_palette.TextSecondary),
                Margin = new Thickness(0, 6, 0, 4)
            });
            TextBox tb = new TextBox
            {
                Text = initialValue ?? "",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                Padding = new Thickness(10, 7, 10, 7),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                CaretBrush = ThemeManager.Brush(_palette.TextPrimary),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                Margin = new Thickness(0, 0, 0, 6)
            };
            parent.Children.Add(tb);
            return tb;
        }

        private Button MakePrimaryBtn(string text, Action click)
        {
            Button b = new Button
            {
                Content = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = ThemeManager.Brush(_palette.Accent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 8, 14, 8),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };
            b.Click += delegate { click(); };
            return b;
        }

        private Border MakeSmallBtn(string glyph, Action click)
        {
            Border b = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(7),
                Margin = new Thickness(4, 0, 0, 0),
                Background = ThemeManager.Brush(Color.FromArgb(35, 148, 163, 184)),
                Cursor = Cursors.Hand
            };
            b.Child = new TextBlock
            {
                Text = glyph,
                FontSize = 12,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            b.MouseLeftButtonUp += delegate { click(); };
            return b;
        }
    }
}
