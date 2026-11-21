using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Reflection;
using Forms = System.Windows.Forms;

namespace MonolithSetup
{
    /// <summary>
    /// Modern dark frosted-glass Monolith Installer & Uninstaller wizard UI.
    /// </summary>
    public class SetupWindow : Window
    {
        private static readonly Color BgModal = Color.FromArgb(248, 16, 18, 26);
        private static readonly Color BgSurface = Color.FromArgb(255, 25, 29, 41);
        private static readonly Color BorderCol = Color.FromArgb(72, 255, 255, 255);
        private static readonly Color AccentBlue = Color.FromRgb(59, 130, 246);
        private static readonly Color AccentGreen = Color.FromRgb(34, 197, 94);
        private static readonly Color AccentRed = Color.FromRgb(239, 68, 68);
        private static readonly Color TextPrim = Color.FromRgb(248, 250, 252);
        private static readonly Color TextSec = Color.FromRgb(186, 198, 218);
        private static readonly Color TextMut = Color.FromRgb(132, 146, 170);

        private readonly bool _uninstallMode;
        private readonly InstallOptions _opts = new InstallOptions();

        private ContentControl _bodyHost;
        private TextBox _dirInput;
        private Border _progressFill;
        private TextBlock _progressStatus;
        private TextBlock _progressPct;
        private bool _removeUserData = false;
        private bool _busy = false;

        public SetupWindow(bool uninstallMode, string overrideDir)
        {
            _uninstallMode = uninstallMode;
            if (!string.IsNullOrEmpty(overrideDir))
                _opts.InstallDir = overrideDir;
            else if (InstallerCore.IsAlreadyInstalled())
                _opts.InstallDir = InstallerCore.GetInstalledLocation();

            Title = uninstallMode ? "Monolith Kaldır" : "Monolith Kurulum";
            Width = 576;
            Height = uninstallMode ? 410 : 548;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;

            PreviewKeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Escape && !_busy) Close();
            };

            BuildShell();
            if (_uninstallMode) ShowUninstallView();
            else ShowInstallView();
        }

        private static SolidColorBrush B(Color c)
        {
            SolidColorBrush b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        private void BuildShell()
        {
            Border shell = new Border
            {
                CornerRadius = new CornerRadius(22),
                Background = B(BgModal),
                BorderBrush = B(BorderCol),
                BorderThickness = new Thickness(1.2),
                Margin = new Thickness(16),
                Effect = new DropShadowEffect { BlurRadius = 34, ShadowDepth = 10, Opacity = 0.6, Color = Colors.Black }
            };

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Header bar with working X close button (PreviewMouseLeftButtonDown)
            Border hdr = new Border
            {
                BorderBrush = B(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(20, 0, 16, 0),
                Background = Brushes.Transparent
            };
            DockPanel dp = new DockPanel { VerticalAlignment = VerticalAlignment.Center };

            StackPanel brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            brand.Children.Add(BuildMiniLogo(26));
            brand.Children.Add(new TextBlock
            {
                Text = _uninstallMode ? "Monolith Kaldırıcı" : "Monolith Kurulum Sihirbazı",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14.5,
                FontWeight = FontWeights.Bold,
                Foreground = B(TextPrim),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            });
            DockPanel.SetDock(brand, Dock.Left);
            dp.Children.Add(brand);

            TextBlock xTxt = new TextBlock
            {
                Text = "\u2715",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = B(TextSec),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Border closeBtn = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                Background = B(BgSurface),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                Child = xTxt
            };
            closeBtn.MouseEnter += delegate { closeBtn.Background = B(AccentRed); xTxt.Foreground = Brushes.White; };
            closeBtn.MouseLeave += delegate { closeBtn.Background = B(BgSurface); xTxt.Foreground = B(TextSec); };
            closeBtn.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (!_busy) Close();
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

            _bodyHost = new ContentControl { Margin = new Thickness(24, 18, 24, 22) };
            Grid.SetRow(_bodyHost, 1);
            root.Children.Add(_bodyHost);

            shell.Child = root;
            Content = shell;
        }

        /// <summary>
        /// Iconic Monolith emblem: circular Spatial dark-crystal orb with a luminous frosted vertical monolith slab.
        /// </summary>
        private UIElement BuildMiniLogo(double size)
        {
            try
            {
                using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream("MonolithIcon.ico"))
                {
                    if (st != null)
                    {
                        IconBitmapDecoder dec = new IconBitmapDecoder(st, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                        BitmapFrame best = null;
                        for (int i = 0; i < dec.Frames.Count; i++)
                        {
                            if (best == null || dec.Frames[i].PixelWidth > best.PixelWidth)
                                best = dec.Frames[i];
                        }
                        if (best != null)
                        {
                            Image img = new Image
                            {
                                Source = best,
                                Width = size,
                                Height = size,
                                Stretch = Stretch.Uniform
                            };
                            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                            return img;
                        }
                    }
                }
            }
            catch { }

            Border b = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2.0),
                BorderBrush = B(Color.FromArgb(165, 255, 255, 255)),
                BorderThickness = new Thickness(1.2)
            };
            LinearGradientBrush grad = new LinearGradientBrush(Color.FromRgb(42, 62, 108), Color.FromRgb(10, 14, 24), 45);
            grad.Freeze();
            b.Background = grad;

            Grid g = new Grid();
            // Glowing vertical Monolith crystal pillar
            Border pillar = new Border
            {
                Width = size * 0.30,
                Height = size * 0.62,
                CornerRadius = new CornerRadius(size * 0.14),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                BorderBrush = B(Color.FromArgb(200, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };
            LinearGradientBrush pGrad = new LinearGradientBrush(Color.FromRgb(250, 252, 255), Color.FromRgb(96, 165, 250), 90);
            pGrad.Freeze();
            pillar.Background = pGrad;
            g.Children.Add(pillar);
            b.Child = g;
            return b;
        }

        // ====== INSTALL VIEW ======
        private void ShowInstallView()
        {
            StackPanel sp = new StackPanel();

            // Hero banner
            Border hero = new Border
            {
                CornerRadius = new CornerRadius(15),
                BorderBrush = B(Color.FromArgb(75, 59, 130, 246)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 0, 14)
            };
            LinearGradientBrush hGrad = new LinearGradientBrush(Color.FromRgb(26, 36, 62), Color.FromRgb(18, 22, 36), 20);
            hGrad.Freeze();
            hero.Background = hGrad;

            StackPanel hRow = new StackPanel { Orientation = Orientation.Horizontal };
            hRow.Children.Add(BuildMiniLogo(46));
            StackPanel hTxt = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            bool isUpgrade = InstallerCore.IsAlreadyInstalled();
            hTxt.Children.Add(new TextBlock
            {
                Text = "Monolith v" + InstallerCore.AppVersion + (isUpgrade ? "  (Güncelleme)" : ""),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = B(TextPrim)
            });
            hTxt.Children.Add(new TextBlock
            {
                Text = "Spatial Glass Görev Çubuğu • 18 İkon Stili • Hareketli Arka Plan • Kontrol Merkezi",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.2,
                Foreground = B(TextSec),
                Margin = new Thickness(0, 3, 0, 0)
            });
            hRow.Children.Add(hTxt);
            hero.Child = hRow;
            sp.Children.Add(hero);

            // Install path picker
            sp.Children.Add(new TextBlock
            {
                Text = "Kurulum Klasörü",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = B(TextSec),
                Margin = new Thickness(2, 0, 0, 5)
            });
            Grid dirRow = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            dirRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dirRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _dirInput = new TextBox
            {
                Text = _opts.InstallDir,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.5,
                Background = B(BgSurface),
                Foreground = B(TextPrim),
                CaretBrush = B(TextPrim),
                BorderBrush = B(BorderCol),
                Padding = new Thickness(10, 7, 10, 7),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_dirInput, 0);
            dirRow.Children.Add(_dirInput);

            Border browse = MakePillBtn("Gözat...", BgSurface, false, delegate
            {
                using (Forms.FolderBrowserDialog fbd = new Forms.FolderBrowserDialog())
                {
                    fbd.SelectedPath = _dirInput.Text;
                    fbd.Description = "Monolith kurulum klasörünü seçin";
                    if (fbd.ShowDialog() == Forms.DialogResult.OK)
                    {
                        string chosen = fbd.SelectedPath;
                        if (!chosen.EndsWith("Monolith", StringComparison.OrdinalIgnoreCase))
                            chosen = Path.Combine(chosen, "Monolith");
                        _dirInput.Text = chosen;
                    }
                }
            });
            browse.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(browse, 1);
            dirRow.Children.Add(browse);
            sp.Children.Add(dirRow);

            // Options toggles
            sp.Children.Add(MakeOptionRow("Masaüstüne Monolith kısayolu oluştur", _opts.CreateDesktopShortcut, delegate(bool v) { _opts.CreateDesktopShortcut = v; }));
            sp.Children.Add(MakeOptionRow("Başlat Menüsüne ve Yüklü Uygulamalara ekle", _opts.CreateStartMenuShortcut, delegate(bool v) { _opts.CreateStartMenuShortcut = v; }));
            sp.Children.Add(MakeOptionRow("Windows açılışında otomatik başlat", _opts.LaunchAtStartup, delegate(bool v) { _opts.LaunchAtStartup = v; }));
            sp.Children.Add(MakeOptionRow("Kurulum tamamlanınca Monolith'ı hemen başlat", _opts.LaunchAfterInstall, delegate(bool v) { _opts.LaunchAfterInstall = v; }));

            // Footer buttons (clean 2-column Grid so text never overlaps button!)
            Grid footer = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock note = new TextBlock
            {
                Text = "Yönetici izni gerekmez • İstediğin zaman Ayarlar'dan kaldırabilirsin",
                FontSize = 10.5,
                Foreground = B(TextMut),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 14, 0)
            };
            Grid.SetColumn(note, 0);
            footer.Children.Add(note);

            UIElement installBtn = MakePillBtn(isUpgrade ? "Güncelle ve Kur" : "Kurulumu Başlat", AccentBlue, true, StartInstall);
            Grid.SetColumn(installBtn, 1);
            footer.Children.Add(installBtn);
            sp.Children.Add(footer);

            _bodyHost.Content = sp;
        }

        private void StartInstall()
        {
            string dir = (_dirInput.Text ?? "").Trim();
            if (string.IsNullOrEmpty(dir)) return;
            _opts.InstallDir = dir;
            _busy = true;
            ShowProgressView("Monolith Kuruluyor...");

            ThreadPool.QueueUserWorkItem(delegate
            {
                string err = null;
                try
                {
                    InstallerCore.Install(_opts, delegate(int pct, string msg)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate { UpdateProgress(pct, msg); }));
                        Thread.Sleep(160);
                    });
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _busy = false;
                    if (err != null)
                        ShowErrorView("Kurulum sırasında hata oluştu:\n" + err);
                    else
                        ShowInstallDoneView();
                }));
            });
        }

        private void ShowInstallDoneView()
        {
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 32, 0, 0) };
            Border checkCircle = new Border
            {
                Width = 64,
                Height = 64,
                CornerRadius = new CornerRadius(32),
                Background = B(Color.FromArgb(45, 34, 197, 94)),
                BorderBrush = B(AccentGreen),
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 16)
            };
            checkCircle.Child = new TextBlock
            {
                Text = "\u2713",
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                Foreground = B(AccentGreen),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(checkCircle);

            sp.Children.Add(new TextBlock
            {
                Text = "Monolith v" + InstallerCore.AppVersion + " Hazır!",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = B(TextPrim),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = "Kurulum konumu: " + _opts.InstallDir + "\nBaşlat Menüsü ve Windows Yüklü Uygulamalar listesine eklendi.",
                FontSize = 12,
                Foreground = B(TextSec),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 8, 0, 24)
            });

            Border finishBtn = MakePillBtn(_opts.LaunchAfterInstall ? "Monolith'ı Başlat ve Bitir" : "Bitir", AccentBlue, true, delegate
            {
                if (_opts.LaunchAfterInstall)
                {
                    try
                    {
                        string exe = Path.Combine(_opts.InstallDir, "Monolith.exe");
                        Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = _opts.InstallDir, UseShellExecute = true });
                    }
                    catch { }
                }
                Close();
            });
            finishBtn.HorizontalAlignment = HorizontalAlignment.Center;
            finishBtn.Padding = new Thickness(26, 11, 26, 11);
            sp.Children.Add(finishBtn);

            _bodyHost.Content = sp;
        }

        // ====== UNINSTALL VIEW ======
        private void ShowUninstallView()
        {
            StackPanel sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = "Monolith'ı Bilgisayardan Kaldır",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = B(TextPrim),
                Margin = new Thickness(0, 4, 0, 6)
            });
            sp.Children.Add(new TextBlock
            {
                Text = "Windows görev çubuğu otomatik olarak geri yüklenecek, kısayollar ve uygulama dosyaları temizlenecektir.\nKonum: " + _opts.InstallDir,
                FontSize = 12,
                Foreground = B(TextSec),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            });

            sp.Children.Add(MakeOptionRow("Kayıtlı ayarlarımı, notlarımı ve görevlerimi de sil (%AppData%\\Monolith)", false, delegate(bool v) { _removeUserData = v; }));

            StackPanel btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 26, 0, 0) };
            Border cancel = MakePillBtn("İptal", BgSurface, false, delegate { Close(); });
            cancel.Margin = new Thickness(0, 0, 10, 0);
            Border removeBtn = MakePillBtn("Monolith'ı Kaldır", AccentRed, true, StartUninstall);
            btns.Children.Add(cancel);
            btns.Children.Add(removeBtn);
            sp.Children.Add(btns);

            _bodyHost.Content = sp;
        }

        private void StartUninstall()
        {
            _busy = true;
            ShowProgressView("Monolith Kaldırılıyor...");

            ThreadPool.QueueUserWorkItem(delegate
            {
                string err = null;
                try
                {
                    InstallerCore.Uninstall(_opts.InstallDir, _removeUserData, delegate(int pct, string msg)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate { UpdateProgress(pct, msg); }));
                        Thread.Sleep(150);
                    });
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                }

                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _busy = false;
                    if (err != null)
                        ShowErrorView("Kaldırma sırasında hata oluştu:\n" + err);
                    else
                        ShowUninstallDoneView();
                }));
            });
        }

        private void ShowUninstallDoneView()
        {
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 32, 0, 0) };
            sp.Children.Add(new TextBlock
            {
                Text = "Monolith Kaldırıldı",
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                Foreground = B(TextPrim),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = "Windows görev çubuğu geri yüklendi ve tüm uygulama kayıtları temizlendi.",
                FontSize = 12,
                Foreground = B(TextSec),
                Margin = new Thickness(0, 8, 0, 22),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            Border done = MakePillBtn("Kapat", AccentBlue, true, delegate { Close(); });
            done.HorizontalAlignment = HorizontalAlignment.Center;
            done.Padding = new Thickness(26, 10, 26, 10);
            sp.Children.Add(done);
            _bodyHost.Content = sp;
        }

        // ====== PROGRESS & ERROR VIEWS ======
        private void ShowProgressView(string heading)
        {
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 48, 8, 0) };
            sp.Children.Add(new TextBlock
            {
                Text = heading,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = B(TextPrim),
                Margin = new Thickness(0, 0, 0, 14)
            });

            Border track = new Border
            {
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Background = B(BgSurface),
                BorderBrush = B(BorderCol),
                BorderThickness = new Thickness(1)
            };
            _progressFill = new Border
            {
                Height = 10,
                CornerRadius = new CornerRadius(5),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 12,
                Background = B(AccentBlue)
            };
            track.Child = _progressFill;
            sp.Children.Add(track);

            DockPanel row = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
            _progressStatus = new TextBlock { Text = "Başlatılıyor...", FontSize = 12, Foreground = B(TextSec) };
            _progressPct = new TextBlock { Text = "0%", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = B(AccentBlue), HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(_progressPct, Dock.Right);
            row.Children.Add(_progressPct);
            row.Children.Add(_progressStatus);
            sp.Children.Add(row);

            _bodyHost.Content = sp;
        }

        private void UpdateProgress(int pct, string msg)
        {
            if (_progressFill != null) _progressFill.Width = Math.Max(12, 478.0 * pct / 100.0);
            if (_progressStatus != null) _progressStatus.Text = msg;
            if (_progressPct != null) _progressPct.Text = pct + "%";
        }

        private void ShowErrorView(string message)
        {
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 24, 0, 0) };
            sp.Children.Add(new TextBlock { Text = "Hata", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = B(AccentRed), Margin = new Thickness(0, 0, 0, 8) });
            sp.Children.Add(new TextBlock { Text = message, FontSize = 12.5, Foreground = B(TextPrim), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20) });
            sp.Children.Add(MakePillBtn("Kapat", BgSurface, false, delegate { Close(); }));
            _bodyHost.Content = sp;
        }

        private UIElement MakeOptionRow(string label, bool initial, Action<bool> onChanged)
        {
            Border row = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = B(BgSurface),
                BorderBrush = B(Color.FromArgb(45, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(13, 9, 13, 9),
                Margin = new Thickness(0, 0, 0, 7),
                Cursor = Cursors.Hand
            };
            DockPanel dp = new DockPanel();
            dp.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.3,
                Foreground = B(TextPrim),
                VerticalAlignment = VerticalAlignment.Center
            });

            Border sw = new Border
            {
                Width = 38,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(2),
                Background = initial ? B(AccentBlue) : B(Color.FromArgb(75, 148, 163, 184)),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Border knob = new Border
            {
                Width = 16,
                Height = 16,
                CornerRadius = new CornerRadius(8),
                Background = Brushes.White,
                HorizontalAlignment = initial ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };
            sw.Child = knob;
            DockPanel.SetDock(sw, Dock.Right);
            dp.Children.Add(sw);
            row.Child = dp;

            bool state = initial;
            row.MouseLeftButtonUp += delegate
            {
                state = !state;
                sw.Background = state ? B(AccentBlue) : B(Color.FromArgb(75, 148, 163, 184));
                knob.HorizontalAlignment = state ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                onChanged(state);
            };
            return row;
        }

        private Border MakePillBtn(string text, Color bgColor, bool primary, Action onClick)
        {
            Color hoverCol = primary
                ? Color.FromRgb((byte)Math.Min(255, bgColor.R + 18), (byte)Math.Min(255, bgColor.G + 18), (byte)Math.Min(255, bgColor.B + 18))
                : Color.FromRgb(36, 42, 60);

            Border b = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = B(bgColor),
                BorderBrush = primary ? Brushes.Transparent : B(BorderCol),
                BorderThickness = new Thickness(primary ? 0 : 1),
                Padding = new Thickness(18, 10, 18, 10),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            b.Child = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = primary ? Brushes.White : B(TextPrim),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            b.MouseEnter += delegate { b.Background = B(hoverCol); };
            b.MouseLeave += delegate { b.Background = B(bgColor); };
            b.MouseLeftButtonUp += delegate { onClick(); };
            return b;
        }
    }
}
