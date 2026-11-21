using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace Monolith
{
    internal class SpotlightResult
    {
        public string Title;
        public string Subtitle;
        public string Category; // "App", "Setting", "File", "Calc", "Web"
        public string Glyph;
        public string IconPath;
        public string FilePath;
        public Action Execute;
    }

    /// <summary>
    /// Windows-11-style integrated Search flyout anchored cleanly ABOVE Monolith's dock (zero taskbar overlap!).
    /// Features:
    /// - Top integrated search bar + filter tabs (All / Apps / Settings / Files / Web)
    /// - Two-column Windows 11 Home view (Recommended apps + Quick Settings searches + Popular Apps grid)
    /// - Real Windows Search Index (Search.CollatorDSO) async file search + Desktop/Downloads/Documents fallback
    /// - Full Start Menu + Registry App Paths + Pinned Apps + 30+ Windows ms-settings shortcuts + Live Calculator
    /// - Right-hand Preview Card with Open & Open File Location actions
    /// </summary>
    public class SpotlightWindow : Window
    {
        private static List<PinnedAppItem> _indexedApps;
        private static bool _indexing;

        private readonly Window _ownerDock;
        private readonly MonolithConfig _cfg;
        private readonly ThemePalette _palette;
        private readonly Action _openWidgets;
        private readonly Action _openSettings;

        private TextBox _searchBox;
        private TextBlock _placeholder;
        private StackPanel _tabsRow;
        private Grid _bodyGrid;
        private StackPanel _resultsPanel;
        private Border _rightPreviewHost;

        private string _activeFilter = "All"; // "All", "App", "Setting", "File", "Web"
        private List<SpotlightResult> _currentResults = new List<SpotlightResult>();
        private List<SpotlightResult> _asyncFileResults = new List<SpotlightResult>();
        private readonly List<Border> _rowBorders = new List<Border>();
        private readonly List<TextBlock> _rowTitles = new List<TextBlock>();
        private int _selectedIndex = 0;
        private int _searchGen = 0;
        private readonly Dictionary<string, ImageSource> _iconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public static void PrewarmIndexAsync()
        {
            if (_indexedApps != null || _indexing) return;
            _indexing = true;
            // Use an STA background thread so late-bound COM Shell.Application ("shell:AppsFolder") works reliably!
            Thread t = new Thread(delegate()
            {
                try
                {
                    List<PinnedAppItem> found = new List<PinnedAppItem>();
                    HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    string[] dirs = new string[]
                    {
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                    };

                    for (int i = 0; i < dirs.Length; i++)
                        ScanShortcuts(dirs[i], found, seen, 0);

                    ScanRegistryAppPaths(Registry.LocalMachine, found, seen);
                    ScanRegistryAppPaths(Registry.CurrentUser, found, seen);
                    ScanShellAppsFolder(found, seen);
                    AddEssentialWindowsCommands(found, seen);

                    found.Sort(delegate(PinnedAppItem a, PinnedAppItem b)
                    {
                        return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    });
                    _indexedApps = found;
                }
                catch { }
                finally { _indexing = false; }
            });
            t.IsBackground = true;
            try { t.SetApartmentState(ApartmentState.STA); } catch { }
            t.Start();
        }

        private static void ScanShellAppsFolder(List<PinnedAppItem> list, HashSet<string> seen)
        {
            object shell = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return;
                shell = Activator.CreateInstance(shellType);
                object folder = shellType.InvokeMember("NameSpace", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { "shell:AppsFolder" });
                if (folder == null) return;
                object items = folder.GetType().InvokeMember("Items", System.Reflection.BindingFlags.InvokeMethod, null, folder, null);
                if (items == null) return;
                int count = Convert.ToInt32(items.GetType().InvokeMember("Count", System.Reflection.BindingFlags.GetProperty, null, items, null));

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        object item = items.GetType().InvokeMember("Item", System.Reflection.BindingFlags.InvokeMethod, null, items, new object[] { i });
                        if (item == null) continue;
                        string name = item.GetType().InvokeMember("Name", System.Reflection.BindingFlags.GetProperty, null, item, null) as string;
                        string path = item.GetType().InvokeMember("Path", System.Reflection.BindingFlags.GetProperty, null, item, null) as string;
                        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path)) continue;

                        string norm = NormalizeSearch(name);
                        if (norm.IndexOf("uninstall", StringComparison.Ordinal) >= 0 ||
                            norm.IndexOf("kaldir", StringComparison.Ordinal) >= 0 ||
                            norm.IndexOf("readme", StringComparison.Ordinal) >= 0)
                            continue;
                        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (seen.Add(name))
                        {
                            bool realFile = File.Exists(path);
                            list.Add(new PinnedAppItem
                            {
                                Name = name,
                                Path = realFile ? path : ("shell:AppsFolder\\" + path),
                                IconPath = realFile ? path : "",
                                Glyph = "\uE71D"
                            });
                        }
                    }
                    catch { }
                }
            }
            catch { }
            finally
            {
                if (shell != null)
                {
                    try { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); } catch { }
                }
            }
        }

        private static void AddEssentialWindowsCommands(List<PinnedAppItem> list, HashSet<string> seen)
        {
            string sys = Environment.SystemDirectory;
            string[,] cmds = new string[,]
            {
                { "Komut İstemi (CMD)",        Path.Combine(sys, "cmd.exe"),          "\uE756" },
                { "Windows PowerShell",        Path.Combine(sys, @"WindowsPowerShell\v1.0\powershell.exe"), "\uE756" },
                { "Denetim Masası (Control)",  Path.Combine(sys, "control.exe"),      "\uE713" },
                { "Kayıt Defteri (Regedit)",   Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "regedit.exe"), "\uE74C" },
                { "Görev Yöneticisi (Taskmgr)",Path.Combine(sys, "Taskmgr.exe"),      "\uE9D9" },
                { "Hesap Makinesi (Calc)",     Path.Combine(sys, "calc.exe"),         "\uE8EF" },
                { "Not Defteri (Notepad)",     Path.Combine(sys, "notepad.exe"),      "\uE70B" },
                { "Paint",                     Path.Combine(sys, "mspaint.exe"),      "\uE790" },
                { "Uzak Masaüstü (MSTSC)",     Path.Combine(sys, "mstsc.exe"),        "\uE7F4" },
                { "Aygıt Yöneticisi",          Path.Combine(sys, "devmgmt.msc"),      "\uE950" },
                { "Hizmetler (Services)",      Path.Combine(sys, "services.msc"),     "\uE90F" },
                { "Disk Yönetimi",             Path.Combine(sys, "diskmgmt.msc"),     "\uEDA2" },
                { "DirectX Tanı (dxdiag)",     Path.Combine(sys, "dxdiag.exe"),       "\uE9D9" }
            };
            for (int i = 0; i < cmds.GetLength(0); i++)
            {
                string name = cmds[i, 0];
                string p = cmds[i, 1];
                string g = cmds[i, 2];
                if (File.Exists(p) && seen.Add(name))
                    list.Add(new PinnedAppItem { Name = name, Path = p, IconPath = p, Glyph = g });
            }
        }

        private static string NormalizeSearch(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            char[] buf = new char[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case 'İ': case 'I': case 'ı': case 'i': buf[i] = 'i'; break;
                    case 'Ğ': case 'ğ': buf[i] = 'g'; break;
                    case 'Ş': case 'ş': buf[i] = 's'; break;
                    case 'Ç': case 'ç': buf[i] = 'c'; break;
                    case 'Ö': case 'ö': buf[i] = 'o'; break;
                    case 'Ü': case 'ü': buf[i] = 'u'; break;
                    default:
                        buf[i] = char.ToLowerInvariant(c);
                        break;
                }
            }
            return new string(buf);
        }

        private static bool MatchesQuery(string candidate, string normQuery)
        {
            if (string.IsNullOrEmpty(normQuery)) return true;
            if (string.IsNullOrEmpty(candidate)) return false;
            string normCand = NormalizeSearch(candidate);
            if (normCand.IndexOf(normQuery, StringComparison.Ordinal) >= 0) return true;
            if (normQuery.IndexOf(' ') > 0)
            {
                string[] terms = normQuery.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < terms.Length; i++)
                {
                    if (normCand.IndexOf(terms[i], StringComparison.Ordinal) < 0)
                        return false;
                }
                return terms.Length > 0;
            }
            return false;
        }

        private static void ScanRegistryAppPaths(RegistryKey rootKey, List<PinnedAppItem> list, HashSet<string> seen)
        {
            try
            {
                using (RegistryKey ap = rootKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths"))
                {
                    if (ap == null) return;
                    string[] subNames = ap.GetSubKeyNames();
                    for (int i = 0; i < subNames.Length; i++)
                    {
                        try
                        {
                            using (RegistryKey sk = ap.OpenSubKey(subNames[i]))
                            {
                                if (sk == null) continue;
                                string p = sk.GetValue("") as string;
                                if (string.IsNullOrEmpty(p)) continue;
                                p = Environment.ExpandEnvironmentVariables(p.Trim().Trim('"'));
                                if (!File.Exists(p)) continue;
                                string name = Path.GetFileNameWithoutExtension(subNames[i]);
                                if (string.IsNullOrEmpty(name)) continue;
                                if (seen.Add(name))
                                    list.Add(new PinnedAppItem { Name = name, Path = p, IconPath = p, Glyph = "\uE71D" });
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static void ScanShortcuts(string dir, List<PinnedAppItem> list, HashSet<string> seen, int depth)
        {
            if (depth > 4 || string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            try
            {
                string[] files = Directory.GetFiles(dir, "*.lnk");
                for (int i = 0; i < files.Length; i++)
                {
                    string f = files[i];
                    string name = Path.GetFileNameWithoutExtension(f);
                    if (string.IsNullOrEmpty(name)) continue;
                    string lower = name.ToLowerInvariant();
                    if (lower.IndexOf("uninstall", StringComparison.Ordinal) >= 0 ||
                        lower.IndexOf("kaldır", StringComparison.Ordinal) >= 0 ||
                        lower.IndexOf("readme", StringComparison.Ordinal) >= 0)
                        continue;

                    if (seen.Add(name))
                        list.Add(new PinnedAppItem { Name = name, Path = f, IconPath = f, Glyph = "\uE71D" });
                }
                string[] subDirs = Directory.GetDirectories(dir);
                for (int i = 0; i < subDirs.Length; i++)
                    ScanShortcuts(subDirs[i], list, seen, depth + 1);
            }
            catch { }
        }

        public SpotlightWindow(Window ownerDock, MonolithConfig cfg, ThemePalette palette, Action openWidgets, Action openSettings)
        {
            _ownerDock = ownerDock;
            _cfg = cfg;
            _palette = palette;
            _openWidgets = openWidgets;
            _openSettings = openSettings;

            PrewarmIndexAsync();

            Title = "Monolith Search";
            Width = 720;
            Height = 540;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;

            BuildUI();

            Deactivated += delegate { try { Close(); } catch { } };
            Loaded += delegate
            {
                PositionAboveDock();
                Activate();
                _searchBox.Focus();
                RefreshBody();
            };
        }

        private void PositionAboveDock()
        {
            double sw = SystemParameters.PrimaryScreenWidth;
            double sh = SystemParameters.PrimaryScreenHeight;
            double left = Math.Round((sw - Width) / 2.0);
            double top;

            if (_ownerDock != null && _ownerDock.ActualHeight > 20)
            {
                double dockCenter = _ownerDock.Left + _ownerDock.ActualWidth / 2.0;
                left = Math.Round(dockCenter - Width / 2.0);
                if (left < 12) left = 12;
                if (left + Width > sw - 12) left = sw - Width - 12;

                if (_cfg.IsTop)
                    top = _ownerDock.Top + _ownerDock.ActualHeight + 2;
                else
                    top = _ownerDock.Top - Height + 8; // Sits cleanly above Monolith with 8px air gap!
            }
            else
            {
                top = Math.Round(sh - Height - 92);
            }

            if (top < 12) top = 12;
            if (top + Height > sh - 12) top = sh - Height - 12;
            Left = left;
            Top = top;
        }

        private void BuildUI()
        {
            Border shell = new Border
            {
                CornerRadius = new CornerRadius(20),
                Background = ThemeManager.Brush(_palette.ModalBg),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(14),
                Effect = new DropShadowEffect { BlurRadius = 34, ShadowDepth = 10, Opacity = 0.56, Color = Colors.Black }
            };

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search input pill
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Filter tabs
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Body

            // 1. Windows 11 Rounded Search Bar right at the top (NEVER detached at bottom!)
            Border topArea = new Border { Padding = new Thickness(18, 16, 18, 8) };
            Border searchPill = new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(_palette.Accent),
                BorderThickness = new Thickness(1.4),
                Padding = new Thickness(14, 9, 10, 9)
            };
            Grid sg = new Grid();
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock searchGlyph = new TextBlock
            {
                Text = "\uE721",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 16,
                Foreground = ThemeManager.Brush(_palette.Accent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(searchGlyph, 0);
            sg.Children.Add(searchGlyph);

            Grid inputGrid = new Grid();
            _placeholder = new TextBlock
            {
                Text = L10n.T(_cfg, "Uygulamalar, ayarlar ve dosyalar için arama yapın...", "Search apps, settings, files and web..."),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14.2,
                Foreground = ThemeManager.Brush(_palette.TextMuted),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            _searchBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14.2,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                CaretBrush = ThemeManager.Brush(_palette.Accent),
                VerticalAlignment = VerticalAlignment.Center
            };
            _searchBox.TextChanged += delegate
            {
                string q = _searchBox.Text ?? "";
                _placeholder.Visibility = string.IsNullOrEmpty(q) ? Visibility.Visible : Visibility.Collapsed;
                TriggerAsyncFileSearch(q);
                RefreshBody();
            };
            _searchBox.PreviewKeyDown += OnSearchKeyDown;
            inputGrid.Children.Add(_placeholder);
            inputGrid.Children.Add(_searchBox);
            Grid.SetColumn(inputGrid, 1);
            sg.Children.Add(inputGrid);

            // Subtle 1-click button to jump to classic OS Win+S if ever wanted
            Border winSBtn = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = Brushes.Transparent,
                Padding = new Thickness(8, 3, 8, 3),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = L10n.T(_cfg, "Klasik Windows Arama'yı aç (Win+S)", "Open classic Windows Search (Win+S)")
            };
            winSBtn.Child = new TextBlock
            {
                Text = "Win+S",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(_palette.TextMuted)
            };
            winSBtn.MouseEnter += delegate { winSBtn.Background = ThemeManager.Brush(Color.FromArgb(35, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B)); };
            winSBtn.MouseLeave += delegate { winSBtn.Background = Brushes.Transparent; };
            winSBtn.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                Close();
                NativeWin32.OpenWindowsSearch();
            };
            Grid.SetColumn(winSBtn, 2);
            sg.Children.Add(winSBtn);

            searchPill.Child = sg;
            topArea.Child = searchPill;
            Grid.SetRow(topArea, 0);
            root.Children.Add(topArea);

            // 2. Windows 11 Filter Tabs Row (All / Apps / Settings / Files / Web)
            Border tabBar = new Border
            {
                BorderBrush = ThemeManager.Brush(_palette.DividerColor),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(18, 2, 18, 9)
            };
            _tabsRow = new StackPanel { Orientation = Orientation.Horizontal };
            tabBar.Child = _tabsRow;
            Grid.SetRow(tabBar, 1);
            root.Children.Add(tabBar);
            BuildFilterTabs();

            // 3. Two-Column Body Area
            _bodyGrid = new Grid { Margin = new Thickness(16, 12, 16, 14) };
            Grid.SetRow(_bodyGrid, 2);
            root.Children.Add(_bodyGrid);

            shell.Child = root;
            Content = shell;
        }

        private void BuildFilterTabs()
        {
            _tabsRow.Children.Clear();
            string[,] defs = new string[,]
            {
                { "All",     L10n.T(_cfg, "Tümü", "All") },
                { "App",     L10n.T(_cfg, "Uygulamalar", "Apps") },
                { "Setting", L10n.T(_cfg, "Ayarlar", "Settings") },
                { "File",    L10n.T(_cfg, "Dosyalar", "Files") },
                { "Web",     "Web" }
            };
            for (int i = 0; i < defs.GetLength(0); i++)
            {
                string key = defs[i, 0];
                string lbl = defs[i, 1];
                bool active = (_activeFilter == key);

                Border tab = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Background = active ? ThemeManager.Brush(_palette.Accent) : Brushes.Transparent,
                    Padding = new Thickness(11, 4.5, 11, 4.5),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = Cursors.Hand
                };
                tab.Child = new TextBlock
                {
                    Text = lbl,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.8,
                    FontWeight = active ? FontWeights.Bold : FontWeights.Medium,
                    Foreground = active ? Brushes.White : ThemeManager.Brush(_palette.TextSecondary)
                };
                if (!active)
                {
                    tab.MouseEnter += delegate { tab.Background = ThemeManager.Brush(_palette.ModalSurface); };
                    tab.MouseLeave += delegate { tab.Background = Brushes.Transparent; };
                }
                tab.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    _activeFilter = key;
                    BuildFilterTabs();
                    if (_activeFilter == "File")
                        TriggerAsyncFileSearch(_searchBox != null ? (_searchBox.Text ?? "") : "");
                    RefreshBody();
                    _searchBox.Focus();
                };
                _tabsRow.Children.Add(tab);
            }
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
            else if (e.Key == Key.Down)
            {
                e.Handled = true;
                if (_currentResults.Count > 0)
                {
                    _selectedIndex = (_selectedIndex + 1) % _currentResults.Count;
                    UpdateSelectionVisuals();
                }
            }
            else if (e.Key == Key.Up)
            {
                e.Handled = true;
                if (_currentResults.Count > 0)
                {
                    _selectedIndex = (_selectedIndex - 1 + _currentResults.Count) % _currentResults.Count;
                    UpdateSelectionVisuals();
                }
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ExecuteSelected();
            }
        }

        private void ExecuteSelected()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _currentResults.Count)
            {
                Action act = _currentResults[_selectedIndex].Execute;
                Close();
                if (act != null) act();
            }
        }

        private void RefreshBody()
        {
            string q = (_searchBox != null ? (_searchBox.Text ?? "") : "").Trim();
            if (string.IsNullOrEmpty(q) && _activeFilter == "All")
            {
                BuildHomeView();
            }
            else
            {
                BuildSearchResults(q);
                RenderSearchModeLayout();
            }
        }

        // ====== WINDOWS 11 TWO-COLUMN HOME VIEW (Recommended + Quick Searches + Popular Apps) ======
        private void BuildHomeView()
        {
            _bodyGrid.Children.Clear();
            _bodyGrid.ColumnDefinitions.Clear();
            _bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(255) });
            _bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            _bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // LEFT COLUMN: "Önerilen" (Recommended apps & quick utilities)
            StackPanel leftCol = new StackPanel();
            leftCol.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, "Önerilen", "Recommended"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                Margin = new Thickness(6, 0, 0, 8)
            });

            List<SpotlightResult> rec = BuildRecommendedItems();
            _currentResults = rec;
            _selectedIndex = 0;

            for (int i = 0; i < rec.Count && i < 8; i++)
            {
                SpotlightResult item = rec[i];
                Border row = new Border
                {
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(8, 6.5, 8, 6.5),
                    Margin = new Thickness(0, 1, 0, 1),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                StackPanel rsp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                rsp.Children.Add(MakeIconVisual(item, 22));
                rsp.Children.Add(new TextBlock
                {
                    Text = item.Title,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12.5,
                    FontWeight = FontWeights.Medium,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                row.Child = rsp;
                row.MouseEnter += delegate { row.Background = ThemeManager.Brush(_palette.ModalSurface); };
                row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
                Action act = item.Execute;
                row.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    Close();
                    if (act != null) act();
                };
                leftCol.Children.Add(row);
            }
            Grid.SetColumn(leftCol, 0);
            _bodyGrid.Children.Add(leftCol);

            // RIGHT COLUMN: "Hızlı aramalar" + "Popüler uygulamalar" (matching Windows 11 Search!)
            StackPanel rightCol = new StackPanel();

            rightCol.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, "Hızlı aramalar", "Quick searches"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                Margin = new Thickness(2, 0, 0, 8)
            });

            Border quickCard = new Border
            {
                CornerRadius = new CornerRadius(13),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 10, 6, 4),
                Margin = new Thickness(0, 0, 0, 16)
            };
            WrapPanel chips = new WrapPanel();
            string[,] quickLinks = new string[,]
            {
                { L10n.T(_cfg, "Odak ayarları", "Focus settings"),          "ms-settings:quiethours" },
                { L10n.T(_cfg, "Ses ayarları", "Sound settings"),           "ms-settings:sound" },
                { L10n.T(_cfg, "Bluetooth ve cihazlar", "Bluetooth"),       "ms-settings:bluetooth" },
                { L10n.T(_cfg, "Görüntü ayarları", "Display settings"),     "ms-settings:display" },
                { L10n.T(_cfg, "Renk ayarları", "Color settings"),          "ms-settings:colors" },
                { L10n.T(_cfg, "Depolama ayarları", "Storage settings"),    "ms-settings:storagesense" }
            };
            for (int i = 0; i < quickLinks.GetLength(0); i++)
            {
                string lbl = quickLinks[i, 0];
                string uri = quickLinks[i, 1];
                Border chip = new Border
                {
                    CornerRadius = new CornerRadius(14),
                    Background = ThemeManager.Brush(Color.FromArgb(_palette.IsLight ? (byte)245 : (byte)42,
                        _palette.IsLight ? (byte)255 : (byte)255,
                        _palette.IsLight ? (byte)255 : (byte)255,
                        _palette.IsLight ? (byte)255 : (byte)255)),
                    BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(11, 5.5, 11, 5.5),
                    Margin = new Thickness(0, 0, 6, 6),
                    Cursor = Cursors.Hand
                };
                chip.Child = new TextBlock
                {
                    Text = lbl,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.5,
                    FontWeight = FontWeights.Medium,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary)
                };
                chip.MouseEnter += delegate { chip.BorderBrush = ThemeManager.Brush(_palette.Accent); };
                chip.MouseLeave += delegate { chip.BorderBrush = ThemeManager.Brush(_palette.ModalBorder); };
                chip.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    Close();
                    LaunchTarget(uri);
                };
                chips.Children.Add(chip);
            }
            quickCard.Child = chips;
            rightCol.Children.Add(quickCard);

            // Popular Apps 3x2 grid
            rightCol.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, "Popüler uygulamalar", "Top apps"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                Margin = new Thickness(2, 0, 0, 8)
            });

            Grid popGrid = new Grid();
            for (int c = 0; c < 3; c++) popGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            popGrid.RowDefinitions.Add(new RowDefinition());
            popGrid.RowDefinitions.Add(new RowDefinition());

            List<SpotlightResult> topApps = BuildTopApps();
            for (int i = 0; i < Math.Min(6, topApps.Count); i++)
            {
                SpotlightResult app = topApps[i];
                int r = i / 3;
                int c = i % 3;
                Border card = new Border
                {
                    Height = 96,
                    CornerRadius = new CornerRadius(12),
                    Background = ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(c == 0 ? 0 : 4, r == 0 ? 0 : 4, c == 2 ? 0 : 4, r == 1 ? 0 : 4),
                    Padding = new Thickness(8),
                    Cursor = Cursors.Hand
                };
                StackPanel csp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                FrameworkElement icoEl = MakeIconVisual(app, 28);
                icoEl.HorizontalAlignment = HorizontalAlignment.Center;
                csp.Children.Add(icoEl);
                csp.Children.Add(new TextBlock
                {
                    Text = app.Title,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.2,
                    FontWeight = FontWeights.Medium,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 7, 0, 0)
                });
                card.Child = csp;
                card.MouseEnter += delegate { card.BorderBrush = ThemeManager.Brush(_palette.Accent); };
                card.MouseLeave += delegate { card.BorderBrush = ThemeManager.Brush(_palette.ModalBorder); };
                Action act = app.Execute;
                card.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    Close();
                    if (act != null) act();
                };
                Grid.SetRow(card, r);
                Grid.SetColumn(card, c);
                popGrid.Children.Add(card);
            }
            rightCol.Children.Add(popGrid);

            Grid.SetColumn(rightCol, 2);
            _bodyGrid.Children.Add(rightCol);
        }

        // ====== LIVE SEARCH RESULTS + RIGHT PREVIEW PANE ======
        private void RenderSearchModeLayout()
        {
            _bodyGrid.Children.Clear();
            _bodyGrid.ColumnDefinitions.Clear();
            _bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            _bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            _bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.85, GridUnitType.Star) });

            ScrollViewer sv = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _resultsPanel = new StackPanel();
            sv.Content = _resultsPanel;
            Grid.SetColumn(sv, 0);
            _bodyGrid.Children.Add(sv);

            _rightPreviewHost = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16)
            };
            Grid.SetColumn(_rightPreviewHost, 2);
            _bodyGrid.Children.Add(_rightPreviewHost);

            RenderSearchModeResults();
        }

        private void RenderSearchModeResults()
        {
            if (_resultsPanel == null) return;
            _resultsPanel.Children.Clear();
            _rowBorders.Clear();
            _rowTitles.Clear();

            for (int i = 0; i < _currentResults.Count; i++)
            {
                int idx = i;
                SpotlightResult r = _currentResults[i];
                bool sel = (i == _selectedIndex);

                Border row = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(10, 7.5, 10, 7.5),
                    Margin = new Thickness(0, 1, 0, 2),
                    Cursor = Cursors.Hand,
                    Background = sel
                        ? ThemeManager.Brush(Color.FromArgb(56, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B))
                        : Brushes.Transparent,
                    BorderBrush = sel
                        ? ThemeManager.Brush(Color.FromArgb(135, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B))
                        : Brushes.Transparent,
                    BorderThickness = new Thickness(1)
                };

                Grid g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                FrameworkElement ico = MakeIconVisual(r, 24);
                ico.Margin = new Thickness(0, 0, 10, 0);
                Grid.SetColumn(ico, 0);
                g.Children.Add(ico);

                StackPanel txts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                TextBlock titleTb = new TextBlock
                {
                    Text = r.Title,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12.8,
                    FontWeight = sel ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                txts.Children.Add(titleTb);
                txts.Children.Add(new TextBlock
                {
                    Text = r.Subtitle,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 10.3,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
                Grid.SetColumn(txts, 1);
                g.Children.Add(txts);

                row.Child = g;
                row.MouseEnter += delegate
                {
                    if (_selectedIndex != idx)
                    {
                        _selectedIndex = idx;
                        UpdateSelectionVisuals();
                    }
                };
                row.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    _selectedIndex = idx;
                    ExecuteSelected();
                };
                _rowBorders.Add(row);
                _rowTitles.Add(titleTb);
                _resultsPanel.Children.Add(row);
            }

            RenderRightPreview();
        }

        private void UpdateSelectionVisuals()
        {
            if (_rowBorders.Count != _currentResults.Count)
            {
                RenderSearchModeResults();
                return;
            }
            Brush selBg = ThemeManager.Brush(Color.FromArgb(56, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B));
            Brush selBr = ThemeManager.Brush(Color.FromArgb(135, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B));
            for (int i = 0; i < _rowBorders.Count; i++)
            {
                bool sel = (i == _selectedIndex);
                _rowBorders[i].Background = sel ? selBg : Brushes.Transparent;
                _rowBorders[i].BorderBrush = sel ? selBr : Brushes.Transparent;
                if (i < _rowTitles.Count)
                    _rowTitles[i].FontWeight = sel ? FontWeights.Bold : FontWeights.SemiBold;
            }
            if (_selectedIndex >= 0 && _selectedIndex < _rowBorders.Count)
                _rowBorders[_selectedIndex].BringIntoView();
            RenderRightPreview();
        }

        private void RenderRightPreview()
        {
            if (_rightPreviewHost == null) return;
            if (_currentResults.Count == 0 || _selectedIndex < 0 || _selectedIndex >= _currentResults.Count)
            {
                _rightPreviewHost.Child = new TextBlock
                {
                    Text = L10n.T(_cfg, "Sonuç bulunamadı", "No results found"),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12.5,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                return;
            }

            SpotlightResult r = _currentResults[_selectedIndex];
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Top };

            Border bigIcoBox = new Border
            {
                Width = 60,
                Height = 60,
                CornerRadius = new CornerRadius(15),
                Background = ThemeManager.Brush(Color.FromArgb(35, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 12)
            };
            bigIcoBox.Child = MakeIconVisual(r, 34);
            sp.Children.Add(bigIcoBox);

            sp.Children.Add(new TextBlock
            {
                Text = r.Title,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });
            sp.Children.Add(new TextBlock
            {
                Text = r.Subtitle,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.2,
                Foreground = ThemeManager.Brush(_palette.TextMuted),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 18)
            });

            // Open primary action button
            Border openBtn = MakePreviewActionBtn("\uE8A7", L10n.T(_cfg, "Aç", "Open"), true, delegate { ExecuteSelected(); });
            sp.Children.Add(openBtn);

            if (!string.IsNullOrEmpty(r.FilePath) && (File.Exists(r.FilePath) || Directory.Exists(r.FilePath)))
            {
                string fp = r.FilePath;
                sp.Children.Add(MakePreviewActionBtn("\uE8B7", L10n.T(_cfg, "Dosya Konumunu Aç", "Open File Location"), false, delegate
                {
                    Close();
                    try { Process.Start("explorer.exe", "/select,\"" + fp + "\""); } catch { }
                }));
            }

            _rightPreviewHost.Child = sp;
        }

        private Border MakePreviewActionBtn(string glyph, string label, bool primary, Action onClick)
        {
            Border b = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = primary ? ThemeManager.Brush(_palette.Accent) : Brushes.Transparent,
                BorderBrush = ThemeManager.Brush(primary ? _palette.Accent : _palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 8),
                Cursor = Cursors.Hand
            };
            StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12.5,
                Foreground = primary ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary),
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = primary ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            });
            b.Child = sp;
            b.MouseEnter += delegate { b.Opacity = 0.88; };
            b.MouseLeave += delegate { b.Opacity = 1.0; };
            b.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                onClick();
            };
            return b;
        }

        // ====== SEARCH ENGINE (Apps + UWP/Store + 30 Windows Settings + Real Windows Search Index Files + Calc + Web) ======
        private void BuildSearchResults(string q)
        {
            List<SpotlightResult> list = new List<SpotlightResult>();
            string normQ = NormalizeSearch(q);

            // 0. Direct local path, ms-settings URI, or URL entered by user
            if (!string.IsNullOrEmpty(q) && (_activeFilter == "All" || _activeFilter == "File" || _activeFilter == "Web"))
            {
                bool isDirectPath = (q.Length >= 3 && q[1] == ':' && (q[2] == '\\' || q[2] == '/')) && (File.Exists(q) || Directory.Exists(q));
                bool isMsUri = q.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase);
                bool isUrl = q.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             q.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                             q.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
                if (isDirectPath || isMsUri || isUrl)
                {
                    string targetCopy = isUrl && q.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? ("https://" + q) : q;
                    list.Add(new SpotlightResult
                    {
                        Title = L10n.T(_cfg, "Aç: ", "Open: ") + q,
                        Subtitle = isDirectPath ? L10n.T(_cfg, "Yerel Konum", "Local Path") : (isMsUri ? "Windows Settings URI" : "Web URL"),
                        Category = isDirectPath ? "File" : (isMsUri ? "Setting" : "Web"),
                        FilePath = isDirectPath ? q : null,
                        IconPath = isDirectPath ? q : null,
                        Glyph = isDirectPath ? "\uE8B7" : (isMsUri ? "\uE713" : "\uE774"),
                        Execute = delegate { LaunchTarget(targetCopy); }
                    });
                }
            }

            // 1. Live math calculator
            double calcVal;
            if ((_activeFilter == "All") && !string.IsNullOrEmpty(q) && TryEvalMath(q, out calcVal))
            {
                string formatted = Math.Abs(calcVal - Math.Round(calcVal)) < 1e-9
                    ? Math.Round(calcVal).ToString("N0", CultureInfo.InvariantCulture)
                    : calcVal.ToString("G10", CultureInfo.InvariantCulture);
                list.Add(new SpotlightResult
                {
                    Title = "= " + formatted,
                    Subtitle = L10n.T(_cfg, "Hesap Makinesi • Enter ile kopyala", "Calculator • Press Enter to copy"),
                    Category = "Calc",
                    Glyph = "\uE8EF",
                    Execute = delegate { try { Clipboard.SetText(formatted); } catch { } }
                });
            }

            // 2. Installed Apps (Pinned + Start Menu + App Paths + shell:AppsFolder UWP/Store + Windows Tools)
            HashSet<string> addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_activeFilter == "All" || _activeFilter == "App")
            {
                for (int i = 0; i < _cfg.PinnedApps.Count; i++)
                {
                    PinnedAppItem p = _cfg.PinnedApps[i];
                    if (MatchesQuery(p.Name, normQ))
                    {
                        if (addedNames.Add(p.Name))
                        {
                            PinnedAppItem cap = p;
                            list.Add(new SpotlightResult
                            {
                                Title = cap.Name,
                                Subtitle = L10n.T(_cfg, "Uygulama (Sabit)", "Pinned App"),
                                Category = "App",
                                IconPath = cap.IconPath,
                                FilePath = cap.Path,
                                Glyph = cap.Glyph,
                                Execute = delegate { LaunchTarget(cap.Path); }
                            });
                        }
                    }
                }

                if (_indexedApps != null)
                {
                    int appCap = (_activeFilter == "App") ? 18 : 10;
                    // Prioritize prefix matches first, then contains/word matches (Turkish-accent-safe!)
                    for (int pass = 0; pass < 2; pass++)
                    {
                        for (int i = 0; i < _indexedApps.Count && list.Count < appCap; i++)
                        {
                            PinnedAppItem app = _indexedApps[i];
                            bool hit;
                            if (string.IsNullOrEmpty(normQ))
                                hit = (pass == 0);
                            else if (pass == 0)
                                hit = NormalizeSearch(app.Name).StartsWith(normQ, StringComparison.Ordinal);
                            else
                                hit = MatchesQuery(app.Name, normQ);

                            if (hit && addedNames.Add(app.Name))
                            {
                                PinnedAppItem cap = app;
                                bool isUwp = !string.IsNullOrEmpty(cap.Path) && cap.Path.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase);
                                list.Add(new SpotlightResult
                                {
                                    Title = cap.Name,
                                    Subtitle = isUwp ? L10n.T(_cfg, "Windows Uygulaması", "Windows App") : L10n.T(_cfg, "Uygulama", "Application"),
                                    Category = "App",
                                    IconPath = cap.IconPath,
                                    FilePath = isUwp ? null : cap.Path,
                                    Glyph = string.IsNullOrEmpty(cap.Glyph) ? "\uE71D" : cap.Glyph,
                                    Execute = delegate { LaunchTarget(cap.Path); }
                                });
                            }
                        }
                    }
                }
            }

            // 3. Windows Settings + System Commands
            if (_activeFilter == "All" || _activeFilter == "Setting")
            {
                int setCap = (_activeFilter == "Setting") ? 24 : 14;
                List<SpotlightResult> settings = BuildWindowsSettingsCatalog();
                for (int i = 0; i < settings.Count && list.Count < setCap; i++)
                {
                    SpotlightResult s = settings[i];
                    if (string.IsNullOrEmpty(normQ) || MatchesQuery(s.Title, normQ) || MatchesQuery(s.Subtitle, normQ))
                    {
                        list.Add(s);
                    }
                }
            }

            // 4. Async Real PC File Results (Windows Search Index / User Folders)
            if ((_activeFilter == "All" || _activeFilter == "File") && _asyncFileResults.Count > 0)
            {
                int fileCap = (_activeFilter == "File") ? 20 : 18;
                for (int i = 0; i < _asyncFileResults.Count && list.Count < fileCap; i++)
                    list.Add(_asyncFileResults[i]);
            }

            // 5. Web Search
            if ((_activeFilter == "All" || _activeFilter == "Web") && !string.IsNullOrEmpty(q))
            {
                string queryCopy = q;
                list.Add(new SpotlightResult
                {
                    Title = L10n.T(_cfg, "Google'da Ara: ", "Search Google: ") + queryCopy,
                    Subtitle = "Web • google.com",
                    Category = "Web",
                    Glyph = "\uE774",
                    Execute = delegate { LaunchTarget("https://www.google.com/search?q=" + Uri.EscapeDataString(queryCopy)); }
                });
                list.Add(new SpotlightResult
                {
                    Title = "YouTube: " + queryCopy,
                    Subtitle = "Web • youtube.com",
                    Category = "Web",
                    Glyph = "\uE714",
                    Execute = delegate { LaunchTarget("https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(queryCopy)); }
                });
            }

            _currentResults = list;
            if (_selectedIndex >= _currentResults.Count) _selectedIndex = 0;
        }

        private void TriggerAsyncFileSearch(string raw)
        {
            string q = (raw ?? "").Trim();
            int gen = Interlocked.Increment(ref _searchGen);
            _asyncFileResults = new List<SpotlightResult>();
            if (q.Length < 2 && _activeFilter != "File") return;

            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(q.Length == 0 ? 15 : 95); // debounce keystrokes
                if (gen != _searchGen) return;

                List<SpotlightResult> hits = QueryWindowsSearchFiles(q);
                if (gen != _searchGen) return;

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (gen != _searchGen) return;
                    _asyncFileResults = hits;
                    if (_activeFilter != "App" && _activeFilter != "Setting" && _activeFilter != "Web")
                    {
                        BuildSearchResults(q);
                        if (_resultsPanel == null) RenderSearchModeLayout();
                        else RenderSearchModeResults();
                    }
                }));
            });
        }

        private List<SpotlightResult> QueryWindowsSearchFiles(string q)
        {
            List<SpotlightResult> results = new List<SpotlightResult>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string normQ = NormalizeSearch(q);

            // 1. Primary: Official Windows Search Index (Search.CollatorDSO) when query >= 2 chars
            if (q.Length >= 2)
            {
                try
                {
                    string safe = q.Replace("'", "''").Replace("%", "").Replace("_", "");
                    using (OleDbConnection conn = new OleDbConnection("Provider=Search.CollatorDSO;Extended Properties='Application=Windows';"))
                    {
                        conn.Open();
                        using (OleDbCommand cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT TOP 10 System.ItemName, System.ItemPathDisplay FROM SYSTEMINDEX WHERE System.ItemName LIKE '%" + safe + "%'";
                            using (OleDbDataReader rdr = cmd.ExecuteReader())
                            {
                                while (rdr.Read() && results.Count < 8)
                                {
                                    string name = rdr[0] as string;
                                    string path = rdr[1] as string;
                                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path)) continue;
                                    if (!seen.Add(path)) continue;
                                    string capPath = path;
                                    results.Add(new SpotlightResult
                                    {
                                        Title = name,
                                        Subtitle = Path.GetDirectoryName(path) ?? path,
                                        Category = "File",
                                        IconPath = path,
                                        FilePath = path,
                                        Glyph = Directory.Exists(path) ? "\uE8B7" : "\uE8A5",
                                        Execute = delegate { LaunchTarget(capPath); }
                                    });
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // 2. Fast local user-folder search (Desktop, Downloads, Documents, OneDrive, Pictures, Videos + 1-level subfolders)
            if (results.Count < 8)
            {
                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string[] folders = new string[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    Path.Combine(profile, "Downloads"),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Path.Combine(profile, @"OneDrive\Desktop"),
                    Path.Combine(profile, @"OneDrive\Documents"),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
                };

                if (string.IsNullOrEmpty(normQ))
                {
                    // Show most recently modified user files when clicking the "Files" tab with an empty search box
                    List<FileSystemInfo> recent = new List<FileSystemInfo>();
                    for (int d = 0; d < 3; d++)
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(folders[d]) || !Directory.Exists(folders[d])) continue;
                            DirectoryInfo di = new DirectoryInfo(folders[d]);
                            FileSystemInfo[] items = di.GetFileSystemInfos();
                            for (int i = 0; i < items.Length && i < 60; i++)
                            {
                                if ((items[i].Attributes & FileAttributes.Hidden) == 0 &&
                                    !items[i].Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                                    recent.Add(items[i]);
                            }
                        }
                        catch { }
                    }
                    recent.Sort(delegate(FileSystemInfo a, FileSystemInfo b) { return b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc); });
                    for (int i = 0; i < recent.Count && results.Count < 10; i++)
                    {
                        string p = recent[i].FullName;
                        if (seen.Add(p))
                        {
                            string capPath = p;
                            results.Add(new SpotlightResult
                            {
                                Title = recent[i].Name,
                                Subtitle = Path.GetDirectoryName(p) ?? p,
                                Category = "File",
                                IconPath = p,
                                FilePath = p,
                                Glyph = Directory.Exists(p) ? "\uE8B7" : "\uE8A5",
                                Execute = delegate { LaunchTarget(capPath); }
                            });
                        }
                    }
                }
                else
                {
                    for (int d = 0; d < folders.Length && results.Count < 10; d++)
                    {
                        ScanLocalFolderMatches(folders[d], normQ, results, seen, 0);
                    }
                }
            }

            return results;
        }

        private static void ScanLocalFolderMatches(string dir, string normQ, List<SpotlightResult> results, HashSet<string> seen, int depth)
        {
            if (depth > 1 || results.Count >= 10 || string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            try
            {
                string[] entries = Directory.GetFileSystemEntries(dir);
                for (int i = 0; i < entries.Length && results.Count < 10; i++)
                {
                    string p = entries[i];
                    string name = Path.GetFileName(p);
                    if (string.IsNullOrEmpty(name) || name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    if (MatchesQuery(name, normQ) && seen.Add(p))
                    {
                        string capPath = p;
                        results.Add(new SpotlightResult
                        {
                            Title = name,
                            Subtitle = Path.GetDirectoryName(p) ?? p,
                            Category = "File",
                            IconPath = p,
                            FilePath = p,
                            Glyph = Directory.Exists(p) ? "\uE8B7" : "\uE8A5",
                            Execute = delegate { LaunchTarget(capPath); }
                        });
                    }
                }
                if (depth == 0 && results.Count < 10)
                {
                    string[] subDirs = Directory.GetDirectories(dir);
                    for (int i = 0; i < subDirs.Length && i < 20 && results.Count < 10; i++)
                    {
                        string bn = Path.GetFileName(subDirs[i]);
                        if (string.IsNullOrEmpty(bn) || bn.StartsWith(".", StringComparison.Ordinal) ||
                            bn.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
                            continue;
                        ScanLocalFolderMatches(subDirs[i], normQ, results, seen, 1);
                    }
                }
            }
            catch { }
        }

        private List<SpotlightResult> BuildRecommendedItems()
        {
            List<SpotlightResult> list = new List<SpotlightResult>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _cfg.PinnedApps.Count && list.Count < 3; i++)
            {
                PinnedAppItem p = _cfg.PinnedApps[i];
                if (seen.Add(p.Name))
                {
                    PinnedAppItem cap = p;
                    list.Add(new SpotlightResult
                    {
                        Title = cap.Name,
                        Subtitle = L10n.T(_cfg, "Uygulama", "Application"),
                        Category = "App",
                        IconPath = cap.IconPath,
                        FilePath = cap.Path,
                        Glyph = cap.Glyph,
                        Execute = delegate { LaunchTarget(cap.Path); }
                    });
                }
            }

            AddBuiltInTool(list, seen, L10n.T(_cfg, "Ekran Alıntısı Aracı", "Snipping Tool"), "\uE7A8", "", "ms-screenclip:");
            AddBuiltInTool(list, seen, L10n.T(_cfg, "Hesap Makinesi", "Calculator"), "\uE8EF", Path.Combine(Environment.SystemDirectory, "calc.exe"), "calc.exe");
            AddBuiltInTool(list, seen, "Paint", "\uE790", Path.Combine(Environment.SystemDirectory, "mspaint.exe"), "mspaint.exe");
            AddBuiltInTool(list, seen, L10n.T(_cfg, "Görev Yöneticisi", "Task Manager"), "\uE9D9", Path.Combine(Environment.SystemDirectory, "Taskmgr.exe"), "taskmgr.exe");
            AddBuiltInTool(list, seen, L10n.T(_cfg, "Windows Ayarları", "Windows Settings"), "\uE713", "", "ms-settings:");
            AddBuiltInTool(list, seen, L10n.T(_cfg, "Monolith Ayarlar", "Monolith Settings"), "\uE713", "", null, _openSettings);
            return list;
        }

        private List<SpotlightResult> BuildTopApps()
        {
            List<SpotlightResult> list = new List<SpotlightResult>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddBuiltInTool(list, seen, L10n.T(_cfg, "Ayarlar", "Settings"), "\uE713", "", "ms-settings:");
            AddBuiltInTool(list, seen, L10n.T(_cfg, "Hesap Makinesi", "Calculator"), "\uE8EF", Path.Combine(Environment.SystemDirectory, "calc.exe"), "calc.exe");
            AddBuiltInTool(list, seen, L10n.T(_cfg, "Ekran Alıntısı", "Snipping Tool"), "\uE7A8", "", "ms-screenclip:");

            for (int i = 0; i < _cfg.PinnedApps.Count && list.Count < 6; i++)
            {
                PinnedAppItem p = _cfg.PinnedApps[i];
                if (seen.Add(p.Name))
                {
                    PinnedAppItem cap = p;
                    list.Add(new SpotlightResult
                    {
                        Title = cap.Name,
                        Subtitle = L10n.T(_cfg, "Uygulama", "App"),
                        Category = "App",
                        IconPath = cap.IconPath,
                        FilePath = cap.Path,
                        Glyph = cap.Glyph,
                        Execute = delegate { LaunchTarget(cap.Path); }
                    });
                }
            }
            AddBuiltInTool(list, seen, L10n.T(_cfg, "Görev Yöneticisi", "Task Manager"), "\uE9D9", Path.Combine(Environment.SystemDirectory, "Taskmgr.exe"), "taskmgr.exe");
            AddBuiltInTool(list, seen, L10n.T(_cfg, "Not Defteri", "Notepad"), "\uE70B", Path.Combine(Environment.SystemDirectory, "notepad.exe"), "notepad.exe");
            return list;
        }

        private void AddBuiltInTool(List<SpotlightResult> list, HashSet<string> seen, string title, string glyph, string iconPath, string target, Action customAct = null)
        {
            if (!seen.Add(title)) return;
            list.Add(new SpotlightResult
            {
                Title = title,
                Subtitle = L10n.T(_cfg, "Sistem Aracı", "System Tool"),
                Category = "App",
                Glyph = glyph,
                IconPath = File.Exists(iconPath) ? iconPath : null,
                FilePath = File.Exists(iconPath) ? iconPath : null,
                Execute = customAct ?? (Action)delegate { LaunchTarget(target); }
            });
        }

        private List<SpotlightResult> BuildWindowsSettingsCatalog()
        {
            List<SpotlightResult> list = new List<SpotlightResult>();
            string[,] entries = new string[,]
            {
                { L10n.T(_cfg, "Windows Ayarları", "Windows Settings"),                 "ayarlar sistem settings",                 "\uE713", "ms-settings:" },
                { L10n.T(_cfg, "Ses Ayarları", "Sound Settings"),                       "ses hoparlor mikrofon sound audio",       "\uE767", "ms-settings:sound" },
                { L10n.T(_cfg, "Bluetooth ve Cihazlar", "Bluetooth & Devices"),         "bluetooth kulaklik fare cihaz devices",   "\uE702", "ms-settings:bluetooth" },
                { L10n.T(_cfg, "Wi-Fi ve Ağ Ayarları", "Wi-Fi & Network"),              "wifi internet ag network ethernet",       "\uE701", "ms-settings:network-wifi" },
                { L10n.T(_cfg, "Görüntü & Ekran Ayarları", "Display Settings"),         "ekran cozunurluk hz parlaklik display",   "\uE7F4", "ms-settings:display" },
                { L10n.T(_cfg, "Gece Işığı", "Night Light"),                            "gece isigi mavi isik night light",        "\uE793", "ms-settings:nightlight" },
                { L10n.T(_cfg, "Renk & Tema Ayarları", "Colors & Personalization"),     "renk tema karanlik koyu colors theme",    "\uE790", "ms-settings:colors" },
                { L10n.T(_cfg, "Odak ve Rahatsız Etme", "Focus Assist"),                "odak bildirim rahatsiz etme focus",       "\uE708", "ms-settings:quiethours" },
                { L10n.T(_cfg, "Pil ve Güç Ayarları", "Power & Battery"),               "pil guc uyku batarya battery power",      "\uE83F", "ms-settings:powersleep" },
                { L10n.T(_cfg, "Windows Update (Güncellemeler)", "Windows Update"),     "guncelleme update",                       "\uE895", "ms-settings:windowsupdate" },
                { L10n.T(_cfg, "Yüklü Uygulamalar", "Installed Apps"),                  "uygulama kaldir program apps",            "\uE71D", "ms-settings:appsfeatures" },
                { L10n.T(_cfg, "Başlangıç Uygulamaları", "Startup Apps"),               "baslangic acilis startup",                "\uE7B5", "ms-settings:startupapps" },
                { L10n.T(_cfg, "Depolama Ayarları", "Storage Settings"),                "disk depolama alan storage",              "\uEDA2", "ms-settings:storagesense" },
                { L10n.T(_cfg, "Görev Çubuğu Ayarları", "Taskbar Settings"),            "gorev cubugu taskbar",                    "\uE74C", "ms-settings:taskbar" },
                { L10n.T(_cfg, "Tarih ve Saat", "Date & Time"),                         "tarih saat bolge date time",              "\uE823", "ms-settings:dateandtime" },
                { L10n.T(_cfg, "Gizlilik ve Güvenlik", "Privacy & Security"),           "guvenlik defender gizlilik privacy",      "\uE72E", "ms-settings:privacy" },
                { L10n.T(_cfg, "Fare Ayarları", "Mouse Settings"),                      "fare hiz imlec mouse",                    "\uE962", "ms-settings:mousetouchpad" },
                { L10n.T(_cfg, "Hakkında (Sistem Bilgisi)", "About PC"),                "sistem ozellikleri ram islemci about",    "\uE946", "ms-settings:about" }
            };

            for (int i = 0; i < entries.GetLength(0); i++)
            {
                string title = entries[i, 0];
                string kw = entries[i, 1];
                string glyph = entries[i, 2];
                string uri = entries[i, 3];
                list.Add(new SpotlightResult
                {
                    Title = title,
                    Subtitle = L10n.T(_cfg, "Windows Ayarı", "Windows Setting") + " • " + kw,
                    Category = "Setting",
                    Glyph = glyph,
                    Execute = delegate { LaunchTarget(uri); }
                });
            }

            list.Add(new SpotlightResult { Title = L10n.T(_cfg, "Monolith Ayarlar", "Monolith Settings"), Subtitle = "monolith tema renk ikon medya", Category = "Setting", Glyph = "\uE713", Execute = _openSettings });
            list.Add(new SpotlightResult { Title = L10n.T(_cfg, "Monolith Widget Galerisi", "Monolith All Widgets"), Subtitle = "monolith widget galeri ekle", Category = "Setting", Glyph = "\uEA86", Execute = _openWidgets });
            list.Add(new SpotlightResult { Title = L10n.T(_cfg, "Görev Yöneticisi", "Task Manager"), Subtitle = "taskmgr cpu ram performans", Category = "Setting", Glyph = "\uE9D9", Execute = delegate { LaunchTarget("taskmgr.exe"); } });
            list.Add(new SpotlightResult { Title = L10n.T(_cfg, "Bilgisayarı Kilitle", "Lock PC"), Subtitle = "kilitle oturum lock", Category = "Setting", Glyph = "\uE72E", Execute = delegate { NativeWin32.LockWorkStation(); } });
            list.Add(new SpotlightResult { Title = L10n.T(_cfg, "Uyku Modu", "Sleep"), Subtitle = "uyku sleep", Category = "Setting", Glyph = "\uE708", Execute = delegate { NativeWin32.PutComputerToSleep(); } });
            return list;
        }

        private FrameworkElement MakeIconVisual(SpotlightResult r, double sz)
        {
            ImageSource src = null;
            if (!string.IsNullOrEmpty(r.IconPath))
            {
                if (!_iconCache.TryGetValue(r.IconPath, out src))
                {
                    src = NativeWin32.ExtractIconFromPath(r.IconPath);
                    _iconCache[r.IconPath] = src;
                }
            }
            if (src != null)
            {
                Image im = new Image
                {
                    Source = src,
                    Width = sz,
                    Height = sz,
                    VerticalAlignment = VerticalAlignment.Center
                };
                RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality);
                return im;
            }
            return new TextBlock
            {
                Text = string.IsNullOrEmpty(r.Glyph) ? "\uE71D" : r.Glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = Math.Round(sz * 0.78),
                Foreground = ThemeManager.Brush(_palette.Accent),
                Width = sz,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static void LaunchTarget(string target)
        {
            try
            {
                if (string.IsNullOrEmpty(target)) return;
                if (target.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", target) { UseShellExecute = true });
                    return;
                }
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch { }
        }

        // ====== Tiny zero-dependency recursive-descent math expression evaluator ======
        private static bool TryEvalMath(string expr, out double result)
        {
            result = 0;
            bool hasOp = false;
            bool hasDigit = false;
            for (int i = 0; i < expr.Length; i++)
            {
                char c = expr[i];
                if (char.IsDigit(c)) hasDigit = true;
                else if (c == '+' || c == '-' || c == '*' || c == '/' || c == '^' || c == '%' || c == 'x' || c == 'X') hasOp = true;
                else if (c != ' ' && c != '.' && c != ',' && c != '(' && c != ')') return false;
            }
            if (!hasDigit || !hasOp) return false;

            try
            {
                string clean = expr.Replace(',', '.').Replace('x', '*').Replace('X', '*');
                int pos = 0;
                double val = ParseAddSub(clean, ref pos);
                SkipSpaces(clean, ref pos);
                if (pos != clean.Length || double.IsNaN(val) || double.IsInfinity(val)) return false;
                result = val;
                return true;
            }
            catch { return false; }
        }

        private static void SkipSpaces(string s, ref int pos)
        {
            while (pos < s.Length && s[pos] == ' ') pos++;
        }

        private static double ParseAddSub(string s, ref int pos)
        {
            double v = ParseMulDiv(s, ref pos);
            while (true)
            {
                SkipSpaces(s, ref pos);
                if (pos >= s.Length) break;
                char op = s[pos];
                if (op == '+' || op == '-')
                {
                    pos++;
                    double rhs = ParseMulDiv(s, ref pos);
                    v = (op == '+') ? (v + rhs) : (v - rhs);
                }
                else break;
            }
            return v;
        }

        private static double ParseMulDiv(string s, ref int pos)
        {
            double v = ParsePower(s, ref pos);
            while (true)
            {
                SkipSpaces(s, ref pos);
                if (pos >= s.Length) break;
                char op = s[pos];
                if (op == '*' || op == '/' || op == '%')
                {
                    pos++;
                    double rhs = ParsePower(s, ref pos);
                    if (op == '*') v *= rhs;
                    else if (op == '/') v /= rhs;
                    else v %= rhs;
                }
                else break;
            }
            return v;
        }

        private static double ParsePower(string s, ref int pos)
        {
            double v = ParseUnary(s, ref pos);
            SkipSpaces(s, ref pos);
            if (pos < s.Length && s[pos] == '^')
            {
                pos++;
                double exp = ParsePower(s, ref pos);
                v = Math.Pow(v, exp);
            }
            return v;
        }

        private static double ParseUnary(string s, ref int pos)
        {
            SkipSpaces(s, ref pos);
            if (pos < s.Length && s[pos] == '-') { pos++; return -ParseUnary(s, ref pos); }
            if (pos < s.Length && s[pos] == '+') { pos++; return ParseUnary(s, ref pos); }
            if (pos < s.Length && s[pos] == '(')
            {
                pos++;
                double inner = ParseAddSub(s, ref pos);
                SkipSpaces(s, ref pos);
                if (pos >= s.Length || s[pos] != ')') throw new FormatException();
                pos++;
                return inner;
            }
            int start = pos;
            while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.')) pos++;
            if (pos == start) throw new FormatException();
            return double.Parse(s.Substring(start, pos - start), CultureInfo.InvariantCulture);
        }
    }
}
