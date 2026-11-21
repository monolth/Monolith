using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Monolith
{
    internal class OpenWindowInfo
    {
        public IntPtr Handle;
        public uint ProcessId;
        public string Title;
        public string ProcessName;
        public string ExePath;
        public string AppUserModelId;
        public bool IsForeground;
    }

    /// <summary>
    /// Tiny floating glass tooltip pill anchored above/below hovered dock items ("Names on hover").
    /// </summary>
    internal class HoverTooltipWindow : Window
    {
        private readonly Border _pill;
        private readonly TextBlock _txt;

        public HoverTooltipWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            IsHitTestVisible = false;
            SizeToContent = SizeToContent.WidthAndHeight;

            _txt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold
            };
            _pill = new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 5, 10, 5),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(8),
                Child = _txt,
                Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Opacity = 0.4, Color = Colors.Black }
            };
            Content = _pill;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int ex = NativeWin32.GetWindowLong(hwnd, NativeWin32.GWL_EXSTYLE);
            NativeWin32.SetWindowLong(hwnd, NativeWin32.GWL_EXSTYLE, ex | NativeWin32.WS_EX_TOOLWINDOW | NativeWin32.WS_EX_NOACTIVATE);
        }

        public void ShowTip(string text, FrameworkElement target, Window dock, bool isTop, ThemePalette palette)
        {
            if (string.IsNullOrEmpty(text) || target == null) return;
            try
            {
                _txt.Text = text;
                _txt.Foreground = ThemeManager.Brush(palette.TooltipText);
                _pill.Background = ThemeManager.Brush(palette.TooltipBg);
                _pill.BorderBrush = ThemeManager.Brush(palette.CardBorder);

                if (!IsVisible) Show();
                UpdateLayout();

                Point pt = target.PointToScreen(new Point(target.ActualWidth / 2.0, 0));
                PresentationSource src = PresentationSource.FromVisual(dock);
                double dpiX = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformToDevice.M11 : 1.0;

                double cx = pt.X / dpiX;
                double left = cx - ActualWidth / 2.0;
                double sw = SystemParameters.PrimaryScreenWidth;
                if (left < 4) left = 4;
                if (left + ActualWidth > sw - 4) left = sw - ActualWidth - 4;

                Left = left;
                Top = isTop ? (dock.Top + dock.ActualHeight - 2) : (dock.Top - ActualHeight + 4);
            }
            catch { }
        }

        public void HideTip()
        {
            if (IsVisible) Hide();
        }
    }

    /// <summary>
    /// Proactive glass toast banner anchored directly above/below the dock (like monolith.app alerts).
    /// </summary>
    internal class GlassToastWindow : Window
    {
        private readonly DispatcherTimer _hideTimer;

        public GlassToastWindow(string title, string message, Window dock, bool isTop, ThemePalette palette)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            SizeToContent = SizeToContent.WidthAndHeight;

            Border box = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = ThemeManager.Brush(palette.ModalBg),
                BorderBrush = ThemeManager.Brush(palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(10),
                MaxWidth = 360,
                Effect = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 6, Opacity = 0.45, Color = Colors.Black }
            };
            StackPanel sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = title,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(palette.Accent)
            });
            sp.Children.Add(new TextBlock
            {
                Text = message,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = ThemeManager.Brush(palette.TextPrimary),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            box.Child = sp;
            Content = box;
            MouseLeftButtonUp += delegate { Close(); };

            Loaded += delegate
            {
                double sw = SystemParameters.PrimaryScreenWidth;
                Left = (sw - ActualWidth) / 2.0;
                Top = isTop ? (dock.Top + dock.ActualHeight + 4) : (dock.Top - ActualHeight - 2);
            };

            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.2) };
            _hideTimer.Tick += delegate { _hideTimer.Stop(); try { Close(); } catch { } };
            _hideTimer.Start();
        }
    }

    /// <summary>
    /// Dual-mode horizontal bar layout:
    /// - Floating Capsule mode: packs Apps | Divider | Widgets+Tray tightly inside the floating pill.
    /// - Full Width (Edge-to-Edge) mode: anchors Widgets+Clock+Tray flush to the RIGHT edge and places
    ///   Start + Search + Apps dead-CENTER (Windows 11) or LEFT (Classic) with automatic collision avoidance!
    /// </summary>
    internal class DockBarLayoutPanel : Panel
    {
        public bool IsFullWidth { get; set; }
        public bool AlignAppsLeft { get; set; }
        public double TargetFullWidth { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            double maxH = 0;
            for (int i = 0; i < InternalChildren.Count; i++)
            {
                UIElement ch = InternalChildren[i];
                ch.Measure(new Size(double.PositiveInfinity, availableSize.Height));
                if (ch.Visibility != Visibility.Collapsed && ch.DesiredSize.Height > maxH)
                    maxH = ch.DesiredSize.Height;
            }

            if (!IsFullWidth || InternalChildren.Count < 3)
            {
                double sumW = 0;
                for (int i = 0; i < InternalChildren.Count; i++)
                {
                    if (InternalChildren[i].Visibility != Visibility.Collapsed)
                        sumW += InternalChildren[i].DesiredSize.Width;
                }
                return new Size(sumW, maxH);
            }

            double appsW = InternalChildren[0].DesiredSize.Width;
            double rightW = InternalChildren[2].DesiredSize.Width;
            double minNeeded = appsW + rightW + 24;
            double targetW = TargetFullWidth > 200 ? TargetFullWidth : (double.IsInfinity(availableSize.Width) ? minNeeded : availableSize.Width);
            return new Size(Math.Max(targetW, minNeeded), maxH);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (!IsFullWidth || InternalChildren.Count < 3)
            {
                double x = 0;
                for (int i = 0; i < InternalChildren.Count; i++)
                {
                    UIElement ch = InternalChildren[i];
                    if (ch.Visibility == Visibility.Collapsed) continue;
                    double w = ch.DesiredSize.Width;
                    double h = ch.DesiredSize.Height;
                    double y = Math.Max(0, (finalSize.Height - h) / 2.0);
                    ch.Arrange(new Rect(x, y, w, h));
                    x += w;
                }
                return finalSize;
            }

            UIElement apps = InternalChildren[0];
            UIElement divider = InternalChildren[1];
            UIElement right = InternalChildren[2];

            double appsW = apps.DesiredSize.Width;
            double appsH = apps.DesiredSize.Height;
            double rightW = right.DesiredSize.Width;
            double rightH = right.DesiredSize.Height;

            // Hide center divider in Full Width mode (clear spatial separation instead)
            divider.Arrange(new Rect(0, 0, 0, 0));

            // 1. Anchor Widgets + Clock + Control Center + Tray flush to the FAR RIGHT edge
            double rightX = Math.Max(0, finalSize.Width - rightW);
            right.Arrange(new Rect(rightX, Math.Max(0, (finalSize.Height - rightH) / 2.0), rightW, rightH));

            // 2. Place Start + Search + Pinned + Open Apps centered (Windows 11) or left-aligned, never colliding with right!
            double appsX;
            if (AlignAppsLeft)
            {
                appsX = 0;
            }
            else
            {
                appsX = Math.Round((finalSize.Width - appsW) / 2.0);
                if (appsX + appsW + 18 > rightX)
                    appsX = Math.Max(0, rightX - appsW - 18);
                if (appsX < 0) appsX = 0;
            }
            apps.Arrange(new Rect(appsX, Math.Max(0, (finalSize.Height - appsH) / 2.0), appsW, appsH));

            return finalSize;
        }
    }

    public class DockWindow : Window
    {
        private const int HOTKEY_TOGGLE = 9101;
        private const int HOTKEY_WIDGETS = 9102;
        private const int HOTKEY_SETTINGS = 9103;
        private const int HOTKEY_SPOTLIGHT = 9104;

        private readonly MonolithConfig _cfg;
        private readonly SystemTelemetry _tel;
        private ThemePalette _palette;
        private DockMetrics _metrics;

        private Border _shadowHost;
        private Border _backdropBorder;
        private AnimatedBackdropHost _mediaBackdrop;
        private Border _specularBorder;
        private DockBarLayoutPanel _barLayout;
        private StackPanel _pinnedPanel;
        private Border _winDivider;
        private StackPanel _windowsPanel;
        private Border _widgetDivider;
        private StackPanel _widgetsPanel;
        private StackPanel _trailingPanel;

        private readonly List<WidgetCardHandle> _widgetHandles = new List<WidgetCardHandle>();
        private readonly List<Border> _magnifyTiles = new List<Border>();
        private readonly Dictionary<string, ImageSource> _iconCache = new Dictionary<string, ImageSource>();
        private readonly Dictionary<IntPtr, DateTime> _closingWindows = new Dictionary<IntPtr, DateTime>();
        private string _lastWindowSignature = "";
        private string _lastBackdropSignature = "";
        private bool _editMode = false;
        private bool _userHidden = false;
        private bool _fullscreenHidden = false;
        private bool _searchHostHidden = false;
        private bool _smartOverlapActive = false;
        private bool _autoHidePeeking = true;
        private int _autoHideCooldown = 0;
        private bool _trayFlyoutActive = false;
        private bool _trayFlyoutOpening = false;
        private bool _tempNativeTaskbarShown = false;
        private DateTime _trayFlyoutStartedUtc = DateTime.MinValue;
        private Border _trayChevronBtn;
        private int _trayAnchorX;
        private int _trayDockTopPx;
        private int _trayDockBottomPx;

        // Drag-to-reorder state
        private string _draggingWidgetId = null;
        private Point _dragStartPoint;

        private readonly DispatcherTimer _fastTimer;
        private readonly DispatcherTimer _windowPollTimer;
        private readonly DispatcherTimer _hoverPollTimer;
        private readonly HoverTooltipWindow _tooltip;
        private readonly WindowPreviewPopup _livePreview;
        private PopoverWindow _activePopover;
        private ControlCenterWindow _controlCenterWin;
        private SpotlightWindow _spotlightWin;
        private DateTime _spotlightClosedUtc = DateTime.MinValue;
        private WidgetPickerWindow _pickerWin;
        private SettingsWindow _settingsWin;
        private Forms.NotifyIcon _trayIcon;
        private bool _exiting = false;

        public DockWindow()
        {
            _cfg = ConfigStore.Load();
            _tel = new SystemTelemetry(_cfg);
            _tel.ToastRequested += delegate(string title, string msg)
            {
                Dispatcher.BeginInvoke(new Action(delegate { ShowToast(title, msg); }));
            };

            _tooltip = new HoverTooltipWindow();
            _livePreview = new WindowPreviewPopup(
                delegate(IntPtr h) { ToggleFocusWindow(h); },
                delegate(IntPtr h) { CloseWindowFast(h); });
            SpotlightWindow.PrewarmIndexAsync();

            Title = "Monolith";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            AllowDrop = true;
            SizeToContent = SizeToContent.WidthAndHeight;

            Drop += OnDockFileDrop;
            DragOver += delegate(object s, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    e.Effects = DragDropEffects.Copy;
                else
                    e.Effects = DragDropEffects.None;
                e.Handled = true;
            };

            MouseMove += OnDockMouseMoveMagnify;
            MouseLeave += delegate { ResetDockMagnification(); };
            MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                if (!e.Handled) ShowDockContextMenu();
            };

            BuildShellStructure();
            RebuildDockItems();
            SetupTrayIcon();

            _fastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _fastTimer.Tick += OnFastTick;
            _fastTimer.Start();

            _windowPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
            _windowPollTimer.Tick += delegate { RefreshOpenWindowsIfChanged(); };
            _windowPollTimer.Start();

            _hoverPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _hoverPollTimer.Tick += OnAutoHidePoll;
            _hoverPollTimer.Start();

            SizeChanged += delegate
            {
                PositionDock();
                UpdateBackdrop(false);
            };

            Loaded += delegate
            {
                if (_cfg.HideWindowsTaskbar)
                {
                    NativeWin32.HideNativeTaskbars();
                    SyncDesktopWorkArea();
                }
                PositionDock();
                UpdateBackdrop(true);
                ShowToast("Monolith", L10n.T(_cfg, "Sağ tıkla: Tüm Widget'lar veya Ayarlar  \u2022  Ctrl+Shift+W", "Right-click for All Widgets or Settings  \u2022  Ctrl+Shift+W"));
            };

            Closing += delegate
            {
                CleanupAndRestore();
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int ex = NativeWin32.GetWindowLong(hwnd, NativeWin32.GWL_EXSTYLE);
            NativeWin32.SetWindowLong(hwnd, NativeWin32.GWL_EXSTYLE, ex | NativeWin32.WS_EX_TOOLWINDOW);

            HwndSource src = HwndSource.FromHwnd(hwnd);
            if (src != null) src.AddHook(WndProc);

            NativeWin32.RegisterHotKey(hwnd, HOTKEY_TOGGLE, NativeWin32.MOD_CONTROL | NativeWin32.MOD_SHIFT | NativeWin32.MOD_NOREPEAT, NativeWin32.VK_F11);
            NativeWin32.RegisterHotKey(hwnd, HOTKEY_WIDGETS, NativeWin32.MOD_CONTROL | NativeWin32.MOD_SHIFT | NativeWin32.MOD_NOREPEAT, NativeWin32.VK_W);
            NativeWin32.RegisterHotKey(hwnd, HOTKEY_SETTINGS, NativeWin32.MOD_CONTROL | NativeWin32.MOD_SHIFT | NativeWin32.MOD_NOREPEAT, NativeWin32.VK_S);
            NativeWin32.RegisterHotKey(hwnd, HOTKEY_SPOTLIGHT, NativeWin32.MOD_CONTROL | NativeWin32.MOD_SHIFT | NativeWin32.MOD_NOREPEAT, NativeWin32.VK_SPACE_KEY);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeWin32.WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_TOGGLE)
                {
                    _userHidden = !_userHidden;
                    ApplyVisibility();
                    handled = true;
                }
                else if (id == HOTKEY_WIDGETS)
                {
                    OpenWidgetPicker();
                    handled = true;
                }
                else if (id == HOTKEY_SETTINGS)
                {
                    OpenSettings();
                    handled = true;
                }
                else if (id == HOTKEY_SPOTLIGHT)
                {
                    OpenSpotlight();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void BuildShellStructure()
        {
            _shadowHost = new Border
            {
                Margin = new Thickness(18, 16, 18, 18),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 36,
                    ShadowDepth = 12,
                    Opacity = 0.54,
                    Color = Colors.Black
                }
            };

            _backdropBorder = new Border
            {
                BorderThickness = new Thickness(1.4),
                SnapsToDevicePixels = true
            };

            _specularBorder = new Border
            {
                IsHitTestVisible = true
            };

            ScrollViewer hScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = false
            };
            hScroll.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e)
            {
                if (hScroll.ScrollableWidth > 1)
                {
                    hScroll.ScrollToHorizontalOffset(hScroll.HorizontalOffset - e.Delta * 0.65);
                    e.Handled = true;
                }
            };

            _barLayout = new DockBarLayoutPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            StackPanel appsGroup = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _pinnedPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _winDivider = new Border { Width = 1, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
            _windowsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            appsGroup.Children.Add(_pinnedPanel);
            appsGroup.Children.Add(_winDivider);
            appsGroup.Children.Add(_windowsPanel);

            _widgetDivider = new Border { Width = 1, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };

            StackPanel rightGroup = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _widgetsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _trailingPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            rightGroup.Children.Add(_widgetsPanel);
            rightGroup.Children.Add(_trailingPanel);

            _barLayout.Children.Add(appsGroup);
            _barLayout.Children.Add(_widgetDivider);
            _barLayout.Children.Add(rightGroup);

            hScroll.Content = _barLayout;
            _specularBorder.Child = hScroll;

            _mediaBackdrop = new AnimatedBackdropHost();
            Grid backdropStack = new Grid();
            backdropStack.Children.Add(_mediaBackdrop);
            backdropStack.Children.Add(_specularBorder);

            _backdropBorder.Child = backdropStack;
            _shadowHost.Child = _backdropBorder;
            Content = _shadowHost;
        }

        public void RebuildDockItems()
        {
            _palette = ThemeManager.GetPalette(_cfg);
            _metrics = ThemeManager.GetMetrics(_cfg.DockSize);

            double outerR = _cfg.FullWidth ? 0 : _metrics.OuterRadius;
            _backdropBorder.Height = _metrics.BarHeight;
            _backdropBorder.CornerRadius = new CornerRadius(outerR);
            _backdropBorder.BorderThickness = _cfg.FullWidth
                ? (_cfg.IsTop ? new Thickness(0, 0, 0, 1.1) : new Thickness(0, 1.1, 0, 0))
                : new Thickness(1.4);
            _backdropBorder.BorderBrush = ThemeManager.CreateDockBorderBrush(_palette);
            _backdropBorder.Padding = new Thickness(0);

            double padX = _cfg.FullWidth ? 16 : _metrics.BarPaddingX;
            _specularBorder.CornerRadius = new CornerRadius(outerR);
            _specularBorder.Background = (_cfg.DockMediaMode != "None")
                ? CreateMediaSpecularOverlay()
                : ThemeManager.CreateSpecularHighlightBrush(_palette);
            _specularBorder.Padding = new Thickness(padX, _metrics.BarPaddingY, padX, _metrics.BarPaddingY);

            if (_barLayout != null)
            {
                _barLayout.IsFullWidth = _cfg.FullWidth;
                _barLayout.AlignAppsLeft = (_cfg.FullWidthAlign == "Left");
                _barLayout.TargetFullWidth = _cfg.FullWidth ? Math.Max(320, SystemParameters.PrimaryScreenWidth - padX * 2) : 0;
                _barLayout.InvalidateMeasure();
            }

            if (_mediaBackdrop != null)
            {
                _mediaBackdrop.SetClipRadius(outerR);
                _mediaBackdrop.Configure(_cfg.DockMediaMode, _cfg.DockMediaPath, _cfg.DockMediaOpacity,
                    _cfg.DockMediaFit, _cfg.DockMediaOffsetX, _cfg.DockMediaOffsetY, _cfg.DockMediaZoom);
            }

            _shadowHost.Margin = _cfg.FullWidth ? new Thickness(0) : new Thickness(18, 14, 18, 18);
            _backdropBorder.MaxWidth = _cfg.FullWidth ? SystemParameters.PrimaryScreenWidth : (SystemParameters.PrimaryScreenWidth - 44);
            _backdropBorder.Width = _cfg.FullWidth ? SystemParameters.PrimaryScreenWidth : double.NaN;

            double divH = _metrics.CardHeight * 0.56;
            _winDivider.Height = divH;
            _winDivider.Background = ThemeManager.Brush(_palette.DividerColor);
            _widgetDivider.Height = divH;
            _widgetDivider.Background = ThemeManager.Brush(_palette.DividerColor);

            // 1 & 2. Unified Windows-Taskbar-style Pinned Apps + Non-Duplicate Open Windows
            List<OpenWindowInfo> openWins = ReadOpenWindows();
            RebuildTaskbarAppPanels(openWins);

            // 3. Active Widgets
            _widgetsPanel.Children.Clear();
            _widgetHandles.Clear();
            for (int i = 0; i < _cfg.ActiveWidgets.Count; i++)
            {
                string wid = _cfg.ActiveWidgets[i];
                WidgetCardHandle handle = WidgetFactory.CreateCard(
                    wid,
                    _cfg,
                    _palette,
                    _metrics,
                    _tel,
                    _editMode,
                    OpenPopoverForWidget,
                    RemoveWidgetFromDock,
                    delegate(string tip)
                    {
                        if (_cfg.NamesOnHover && !_editMode)
                            ShowHoverTipFor(tip, wid);
                    },
                    delegate { _tooltip.HideTip(); });

                if (handle != null)
                {
                    AttachWidgetDragHandlers(handle);
                    _widgetHandles.Add(handle);
                    _widgetsPanel.Children.Add(handle.Container);
                }
            }
            _widgetDivider.Visibility = (_widgetsPanel.Children.Count > 0) ? Visibility.Visible : Visibility.Collapsed;

            // 4. Trailing Monolith Control Center, Tray Chevron ('^' Show Hidden Icons), & Settings button
            _trailingPanel.Children.Clear();
            if (_cfg.ShowControlCenterButton)
            {
                Border ccBtn = null;
                ccBtn = BuildUtilityDotButton("\uE9E9", L10n.T(_cfg, "Kontrol Merkezi", "Control Center"), delegate { OpenControlCenter(ccBtn); });
                _trailingPanel.Children.Add(ccBtn);
            }
            string chevronGlyph = _cfg.IsTop ? "\uE70D" : "\uE70E";
            _trayChevronBtn = BuildUtilityDotButton(chevronGlyph, L10n.T(_cfg, "Gizli simgeleri göster", "Show hidden icons"), ToggleHiddenIconsTray);
            _trayChevronBtn.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                ShowTrayChevronMenu();
            };
            _trailingPanel.Children.Add(_trayChevronBtn);
            _trailingPanel.Children.Add(BuildUtilityDotButton("\uE713", L10n.T(_cfg, "Ayarlar", "Settings"), delegate { OpenSettings(); }));

            UpdateLayout();
            PositionDock();
            if (IsLoaded) SyncDesktopWorkArea();
            UpdateBackdrop(true);
        }

        private void SyncDesktopWorkArea()
        {
            if (_exiting) return;
            if (!_cfg.HideWindowsTaskbar || _tempNativeTaskbarShown)
            {
                NativeWin32.RemoveMonolithAppBar();
                return;
            }
            try
            {
                PresentationSource src = PresentationSource.FromVisual(this);
                double dpiY = (src != null && src.CompositionTarget != null) ? src.CompositionTarget.TransformToDevice.M22 : 1.0;
                bool shouldReserve = (_cfg.FullWidth && !_cfg.AutoHide) ||
                                     (!_cfg.FullWidth && _cfg.DockOverlapMode == "ReserveSpace" && !_cfg.AutoHide);
                int reservePx = _cfg.FullWidth
                    ? (int)Math.Round(_metrics.BarHeight * dpiY)
                    : (int)Math.Round((_metrics.BarHeight + _metrics.EdgeMargin + 4) * dpiY);
                NativeWin32.ConfigureMonolithAppBar(shouldReserve, _cfg.IsTop, reservePx);
            }
            catch { }
        }

        private void ShowHoverTipFor(string text, string wid)
        {
            for (int i = 0; i < _widgetHandles.Count; i++)
            {
                if (_widgetHandles[i].Id == wid)
                {
                    _tooltip.ShowTip(text, _widgetHandles[i].Container, this, _cfg.IsTop, _palette);
                    return;
                }
            }
        }

        private void AttachWidgetDragHandlers(WidgetCardHandle handle)
        {
            Border container = handle.Container;
            string wid = handle.Id;

            container.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                _dragStartPoint = e.GetPosition(this);
                _draggingWidgetId = wid;
            };

            container.PreviewMouseMove += delegate(object s, MouseEventArgs e)
            {
                if (_draggingWidgetId == wid && e.LeftButton == MouseButtonState.Pressed)
                {
                    Point cur = e.GetPosition(this);
                    if (Math.Abs(cur.X - _dragStartPoint.X) > 10)
                    {
                        _tooltip.HideTip();
                        DragDrop.DoDragDrop(container, "MONOLITH_WIDGET:" + wid, DragDropEffects.Move);
                        _draggingWidgetId = null;
                    }
                }
            };

            container.AllowDrop = true;
            container.DragOver += delegate(object s, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.StringFormat))
                {
                    string data = e.Data.GetData(DataFormats.StringFormat) as string;
                    if (data != null && data.StartsWith("MONOLITH_WIDGET:"))
                    {
                        e.Effects = DragDropEffects.Move;
                        e.Handled = true;
                    }
                }
            };
            container.Drop += delegate(object s, DragEventArgs e)
            {
                if (e.Data.GetDataPresent(DataFormats.StringFormat))
                {
                    string data = e.Data.GetData(DataFormats.StringFormat) as string;
                    if (data != null && data.StartsWith("MONOLITH_WIDGET:"))
                    {
                        string srcId = data.Substring("MONOLITH_WIDGET:".Length);
                        if (srcId != wid)
                        {
                            int srcIdx = _cfg.ActiveWidgets.IndexOf(srcId);
                            int dstIdx = _cfg.ActiveWidgets.IndexOf(wid);
                            if (srcIdx >= 0 && dstIdx >= 0)
                            {
                                _cfg.ActiveWidgets.RemoveAt(srcIdx);
                                _cfg.ActiveWidgets.Insert(dstIdx, srcId);
                                ConfigStore.Save(_cfg);
                                RebuildDockItems();
                            }
                        }
                        e.Handled = true;
                    }
                }
            };
        }

        private Border BuildStartTile()
        {
            double sz = _metrics.IconTileSize;
            Border tile = CreateIconTileShell(sz);

            // Crisp modern 2x2 Windows grid graphic
            double q = Math.Round(sz * 0.19);
            Grid g = new Grid { Width = q * 2 + 2.5, Height = q * 2 + 2.5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions.Add(new RowDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition());

            Brush b = ThemeManager.Brush(_palette.TextPrimary);
            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 2; c++)
                {
                    Border sq = new Border
                    {
                        Width = q,
                        Height = q,
                        CornerRadius = new CornerRadius(1.6),
                        Background = b,
                        Margin = new Thickness(0.8)
                    };
                    Grid.SetRow(sq, r);
                    Grid.SetColumn(sq, c);
                    g.Children.Add(sq);
                }
            }
            SetOrbContent(tile, g);
            AttachHoverScale(tile, L10n.T(_cfg, "Başlat Menüsü", "Start Menu"));
            tile.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                PlayBounceAnimation(tile);
                NativeWin32.OpenStartMenu();
            };
            tile.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                ShowStartPowerMenu();
            };
            return tile;
        }

        private void ShowStartPowerMenu()
        {
            List<GlassMenuItemDef> items = new List<GlassMenuItemDef>();
            items.Add(new GlassMenuItemDef { IsHeader = true, Label = L10n.T(_cfg, "Başlat • Hızlı Sistem Menüsü", "Start • Power User Menu") });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Yüklü Uygulamalar", "Installed Apps"), Glyph = "\uE71D", OnClick = delegate { try { Process.Start(new ProcessStartInfo("ms-settings:appsfeatures") { UseShellExecute = true }); } catch { } } });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Sistem Bilgisi", "System"), Glyph = "\uE946", OnClick = delegate { try { Process.Start(new ProcessStartInfo("ms-settings:about") { UseShellExecute = true }); } catch { } } });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Görev Yöneticisi", "Task Manager"), Glyph = "\uE9D9", OnClick = delegate { try { Process.Start("taskmgr.exe"); } catch { } } });
            items.Add(new GlassMenuItemDef { Label = "Terminal", Glyph = "\uE756", OnClick = delegate { try { Process.Start(new ProcessStartInfo("wt.exe") { UseShellExecute = true }); } catch { try { Process.Start("powershell.exe"); } catch { } } } });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Dosya Gezgini", "File Explorer"), Glyph = "\uE8B7", OnClick = delegate { try { Process.Start("explorer.exe"); } catch { } } });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Çalıştır", "Run"), Glyph = "\uE768", RightHint = "Win+R", OnClick = delegate { NativeWin32.OpenRunDialog(); } });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Windows Ayarları", "Windows Settings"), Glyph = "\uE713", OnClick = delegate { try { Process.Start(new ProcessStartInfo("ms-settings:") { UseShellExecute = true }); } catch { } } });
            items.Add(new GlassMenuItemDef { IsSeparator = true });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Masaüstünü Göster", "Show Desktop"), Glyph = "\uE7F4", RightHint = "Win+D", OnClick = delegate { NativeWin32.ShowDesktop(); } });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Bilgisayarı Kilitle", "Lock"), Glyph = "\uE72E", OnClick = delegate { NativeWin32.LockWorkStation(); } });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Uyku Modu", "Sleep"), Glyph = "\uE708", OnClick = delegate { NativeWin32.PutComputerToSleep(); } });
            new GlassMenuWindow(items, _palette, this, _cfg.IsTop).Show();
        }

        private static Brush CreateMediaSpecularOverlay()
        {
            LinearGradientBrush lg = new LinearGradientBrush();
            lg.StartPoint = new Point(0, 0);
            lg.EndPoint = new Point(0, 1);
            lg.GradientStops.Add(new GradientStop(Color.FromArgb(42, 255, 255, 255), 0.0));
            lg.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.28));
            lg.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.72));
            lg.GradientStops.Add(new GradientStop(Color.FromArgb(26, 0, 0, 0), 1.0));
            lg.Freeze();
            return lg;
        }

        private void RebuildTaskbarAppPanels(List<OpenWindowInfo> openWins)
        {
            _pinnedPanel.Children.Clear();
            _magnifyTiles.Clear();

            Border startTile = BuildStartTile();
            _pinnedPanel.Children.Add(startTile);
            _magnifyTiles.Add(startTile);

            if (_cfg.ShowSpotlightButton)
            {
                Border searchTile = BuildSpotlightDockTile();
                _pinnedPanel.Children.Add(searchTile);
                _magnifyTiles.Add(searchTile);
            }

            HashSet<IntPtr> claimedByPinned = new HashSet<IntPtr>();
            for (int i = 0; i < _cfg.PinnedApps.Count; i++)
            {
                PinnedAppItem app = _cfg.PinnedApps[i];
                List<OpenWindowInfo> matched = GetMatchedWindowsForPinned(app, openWins);
                for (int m = 0; m < matched.Count; m++)
                    claimedByPinned.Add(matched[m].Handle);

                Border pTile = BuildPinnedAppTile(app, i, matched);
                _pinnedPanel.Children.Add(pTile);
                _magnifyTiles.Add(pTile);
            }

            // Only show open windows that are NOT already represented by a pinned taskbar icon!
            List<OpenWindowInfo> unpinnedWins = new List<OpenWindowInfo>();
            for (int i = 0; i < openWins.Count; i++)
            {
                if (!claimedByPinned.Contains(openWins[i].Handle))
                    unpinnedWins.Add(openWins[i]);
            }
            RebuildOpenWindowsPanel(unpinnedWins);
        }

        private Border BuildSpotlightDockTile()
        {
            double sz = _metrics.IconTileSize;
            Border tile = CreateIconTileShell(sz);
            SetOrbContent(tile, new TextBlock
            {
                Text = "\uE721",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = Math.Round(_metrics.IconImageSize * 0.62),
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
            bool useNativeWinS = (_cfg.SearchMode == "WindowsNative");
            string tip = useNativeWinS
                ? L10n.T(_cfg, "Klasik Windows Arama (Win+S)", "Classic Windows Search (Win+S)")
                : L10n.T(_cfg, "Windows Arama (Ctrl+Shift+Space)", "Windows Search (Ctrl+Shift+Space)");
            AttachHoverScale(tile, tip);
            tile.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                PlayBounceAnimation(tile);
                if (useNativeWinS) NativeWin32.OpenWindowsSearch();
                else OpenSpotlight();
            };
            return tile;
        }

        private Border BuildPinnedAppTile(PinnedAppItem app, int index, List<OpenWindowInfo> matchedWins)
        {
            double sz = _metrics.IconTileSize;
            Border tile = CreateIconTileShell(sz);
            bool running = (matchedWins != null && matchedWins.Count > 0);
            OpenWindowInfo primaryWin = null;
            bool isFg = false;
            if (running)
            {
                primaryWin = matchedWins[0];
                for (int i = 0; i < matchedWins.Count; i++)
                {
                    if (matchedWins[i].IsForeground)
                    {
                        primaryWin = matchedWins[i];
                        isFg = true;
                        break;
                    }
                }
            }

            Grid inner = new Grid();
            ImageSource ico = GetCachedIcon(app.IconPath);
            if (ico == null && primaryWin != null)
                ico = NativeWin32.ExtractIconFromWindow(primaryWin.Handle, primaryWin.ExePath);

            if (ico != null)
            {
                Image img = new Image
                {
                    Source = ico,
                    Width = _metrics.IconImageSize,
                    Height = _metrics.IconImageSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                inner.Children.Add(img);
            }
            else
            {
                inner.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrEmpty(app.Glyph) ? "\uE71D" : app.Glyph,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = Math.Round(_metrics.IconImageSize * 0.65),
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            if (running)
            {
                // Sleek Windows 11-style active taskbar pill indicator (wider accent pill when foreground!)
                Border pill = new Border
                {
                    Width = isFg ? 16 : 7,
                    Height = 3.2,
                    CornerRadius = new CornerRadius(1.6),
                    Background = isFg ? ThemeManager.Brush(_palette.Accent) : ThemeManager.Brush(_palette.TextSecondary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 1.5),
                    Effect = isFg ? new DropShadowEffect
                    {
                        BlurRadius = 7,
                        ShadowDepth = 0,
                        Opacity = 0.9,
                        Color = _palette.Accent
                    } : null
                };
                inner.Children.Add(pill);
            }

            SetOrbContent(tile, inner);
            IconStyleFactory.ApplyVisualState(tile, _palette, false, isFg);

            OpenWindowInfo capPrimary = primaryWin;
            List<OpenWindowInfo> capMatched = matchedWins;
            ImageSource capIco = ico;
            bool capFg = isFg;

            tile.MouseEnter += delegate
            {
                IconStyleFactory.ApplyVisualState(tile, _palette, true, capFg);
                if (!_cfg.DockMagnification)
                    AnimateTileTransform(tile, 1.12, 0, 120);
                if (capPrimary != null && _cfg.LiveWindowPreviews)
                {
                    _tooltip.HideTip();
                    _livePreview.ShowPreview(capPrimary.Handle, capPrimary.Title, capIco, tile, this, _cfg.IsTop, _palette);
                }
                else if (_cfg.NamesOnHover)
                {
                    _tooltip.ShowTip(app.Name, tile, this, _cfg.IsTop, _palette);
                }
            };
            tile.MouseLeave += delegate
            {
                IconStyleFactory.ApplyVisualState(tile, _palette, false, capFg);
                if (!_cfg.DockMagnification)
                    AnimateTileTransform(tile, 1.0, 0, 140);
                _tooltip.HideTip();
                if (capPrimary != null && _cfg.LiveWindowPreviews)
                    _livePreview.ScheduleHide();
            };

            tile.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                _livePreview.HideImmediate();
                if (capMatched != null && capMatched.Count > 0)
                {
                    if (capMatched.Count > 1 && capFg)
                    {
                        // Cycle to next window of same app when already focused
                        int curI = capMatched.IndexOf(capPrimary);
                        OpenWindowInfo nextW = capMatched[(curI + 1) % capMatched.Count];
                        ToggleFocusWindow(nextW.Handle);
                    }
                    else
                    {
                        ToggleFocusWindow(capPrimary.Handle);
                    }
                }
                else
                {
                    PlayBounceAnimation(tile);
                    LaunchNewAppInstance(app);
                }
            };
            tile.MouseDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    e.Handled = true;
                    _livePreview.HideImmediate();
                    PlayBounceAnimation(tile);
                    LaunchNewAppInstance(app);
                }
            };
            tile.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                _livePreview.HideImmediate();
                ShowPinnedAppMenu(app, index, capMatched);
            };

            return tile;
        }

        private Border BuildUtilityDotButton(string mdl2Glyph, string tip, Action onClick)
        {
            double sz = Math.Round(_metrics.IconTileSize * 0.80);
            Border b = IconStyleFactory.CreateTile("Circle", sz, _palette);
            b.Margin = new Thickness(3, 0, 2, 0);
            TextBlock tb = new TextBlock
            {
                Text = mdl2Glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = Math.Round(sz * 0.42),
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            IconStyleFactory.SetContent(b, tb);

            b.MouseEnter += delegate
            {
                b.Background = ThemeManager.Brush(_palette.Accent);
                b.BorderBrush = ThemeManager.CreateOrbRimBrush(_palette, true);
                tb.Foreground = Brushes.White;
                if (_cfg.NamesOnHover) _tooltip.ShowTip(tip, b, this, _cfg.IsTop, _palette);
            };
            b.MouseLeave += delegate
            {
                if (b == _trayChevronBtn && _trayFlyoutActive)
                {
                    b.Background = ThemeManager.Brush(_palette.Accent);
                    tb.Foreground = Brushes.White;
                }
                else
                {
                    IconStyleFactory.ApplyVisualState(b, _palette, false, false);
                    tb.Foreground = ThemeManager.Brush(_palette.TextPrimary);
                }
                _tooltip.HideTip();
            };
            b.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                onClick();
            };
            return b;
        }

        private Border CreateIconTileShell(double sz)
        {
            return IconStyleFactory.CreateTile(_cfg.IconStyle, sz, _palette);
        }

        private static void SetOrbContent(Border orb, UIElement content)
        {
            IconStyleFactory.SetContent(orb, content);
        }

        private void AttachHoverScale(Border tile, string tipText)
        {
            tile.MouseEnter += delegate
            {
                IconStyleFactory.ApplyVisualState(tile, _palette, true, false);
                if (!_cfg.DockMagnification)
                    AnimateTileTransform(tile, 1.12, 0, 120);
                if (_cfg.NamesOnHover && !string.IsNullOrEmpty(tipText))
                    _tooltip.ShowTip(tipText, tile, this, _cfg.IsTop, _palette);
            };
            tile.MouseLeave += delegate
            {
                IconStyleFactory.ApplyVisualState(tile, _palette, false, false);
                if (!_cfg.DockMagnification)
                    AnimateTileTransform(tile, 1.0, 0, 140);
                _tooltip.HideTip();
            };
        }

        /// <summary>
        /// Smooth parabolic wave magnification across neighboring dock icons.
        /// </summary>
        private void OnDockMouseMoveMagnify(object sender, MouseEventArgs e)
        {
            if (!_cfg.DockMagnification || _magnifyTiles.Count == 0) return;

            Point mousePt = e.GetPosition(this);
            double radius = 128.0;
            double liftSign = _cfg.IsTop ? 1.0 : -1.0;

            bool nearAny = false;
            for (int i = 0; i < _magnifyTiles.Count; i++)
            {
                Border t = _magnifyTiles[i];
                if (t.ActualWidth <= 0) continue;
                try
                {
                    Point center = t.TranslatePoint(new Point(t.ActualWidth * 0.5, t.ActualHeight * 0.5), this);
                    double dx = Math.Abs(mousePt.X - center.X);
                    double dy = Math.Abs(mousePt.Y - center.Y);
                    if (dx < radius && dy < 60)
                    {
                        nearAny = true;
                        double norm = dx / radius; // 0 at center, 1 at edge
                        double wave = 0.5 * (1.0 + Math.Cos(norm * Math.PI)); // smooth bell curve
                        double targetScale = 1.0 + 0.20 * wave;
                        double targetLift = liftSign * 3.5 * wave;
                        SetTileTransformImmediate(t, targetScale, targetLift);
                    }
                    else
                    {
                        SetTileTransformImmediate(t, 1.0, 0.0);
                    }
                }
                catch { }
            }

            if (!nearAny)
                ResetDockMagnification();
        }

        private void ResetDockMagnification()
        {
            for (int i = 0; i < _magnifyTiles.Count; i++)
                AnimateTileTransform(_magnifyTiles[i], 1.0, 0.0, 150);
        }

        private static void SetTileTransformImmediate(UIElement el, double scale, double liftY)
        {
            TransformGroup tg = el.RenderTransform as TransformGroup;
            if (tg == null || tg.Children.Count < 2) return;
            ScaleTransform st = tg.Children[0] as ScaleTransform;
            TranslateTransform tt = tg.Children[1] as TranslateTransform;
            if (st != null)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                st.ScaleX = scale;
                st.ScaleY = scale;
            }
            if (tt != null)
            {
                tt.BeginAnimation(TranslateTransform.YProperty, null);
                tt.Y = liftY;
            }
        }

        private static void AnimateTileTransform(UIElement el, double targetScale, double targetY, int ms)
        {
            TransformGroup tg = el.RenderTransform as TransformGroup;
            if (tg == null || tg.Children.Count < 2) return;
            ScaleTransform st = tg.Children[0] as ScaleTransform;
            TranslateTransform tt = tg.Children[1] as TranslateTransform;
            TimeSpan dur = TimeSpan.FromMilliseconds(ms);
            CubicEase ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            if (st != null)
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(targetScale, dur) { EasingFunction = ease });
                st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(targetScale, dur) { EasingFunction = ease });
            }
            if (tt != null)
            {
                tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(targetY, dur) { EasingFunction = ease });
            }
        }

        /// <summary>
        /// Tactile spring bounce when launching an app.
        /// </summary>
        private void PlayBounceAnimation(UIElement el)
        {
            TransformGroup tg = el.RenderTransform as TransformGroup;
            if (tg == null || tg.Children.Count < 2) return;
            TranslateTransform tt = tg.Children[1] as TranslateTransform;
            if (tt == null) return;

            double sign = _cfg.IsTop ? 1.0 : -1.0;
            DoubleAnimationUsingKeyFrames kf = new DoubleAnimationUsingKeyFrames();
            kf.KeyFrames.Add(new EasingDoubleKeyFrame(sign * 7.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
            kf.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
            kf.KeyFrames.Add(new EasingDoubleKeyFrame(sign * 3.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(310))));
            kf.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));
            tt.BeginAnimation(TranslateTransform.YProperty, kf);
        }

        private void RebuildOpenWindowsPanel(List<OpenWindowInfo> wins)
        {
            // Remove old open-window tiles from magnification list (keep pinned tiles)
            for (int i = 0; i < _windowsPanel.Children.Count; i++)
            {
                Border old = _windowsPanel.Children[i] as Border;
                if (old != null) _magnifyTiles.Remove(old);
            }
            _windowsPanel.Children.Clear();
            if (!_cfg.ShowOpenWindows || wins.Count == 0)
            {
                _winDivider.Visibility = Visibility.Collapsed;
                return;
            }

            // Show up to 6 open non-duplicate windows cleanly
            int shown = 0;
            for (int i = 0; i < wins.Count && shown < 6; i++)
            {
                OpenWindowInfo w = wins[i];
                double sz = _metrics.IconTileSize;
                Border tile = CreateIconTileShell(sz);

                Grid g = new Grid();
                ImageSource ico = NativeWin32.ExtractIconFromWindow(w.Handle, w.ExePath);
                if (ico != null)
                {
                    Image img = new Image
                    {
                        Source = ico,
                        Width = _metrics.IconImageSize,
                        Height = _metrics.IconImageSize,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    g.Children.Add(img);
                }
                else
                {
                    g.Children.Add(new TextBlock
                    {
                        Text = w.Title.Length > 0 ? w.Title.Substring(0, 1).ToUpperInvariant() : "W",
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Foreground = ThemeManager.Brush(_palette.TextPrimary),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }

                // Sleek Windows 11-style active taskbar pill indicator
                g.Children.Add(new Border
                {
                    Width = w.IsForeground ? 16 : 7,
                    Height = 3.2,
                    CornerRadius = new CornerRadius(1.6),
                    Background = w.IsForeground ? ThemeManager.Brush(_palette.Accent) : ThemeManager.Brush(_palette.TextSecondary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 1.5),
                    Effect = w.IsForeground ? new DropShadowEffect { BlurRadius = 7, ShadowDepth = 0, Opacity = 0.9, Color = _palette.Accent } : null
                });

                SetOrbContent(tile, g);

                OpenWindowInfo captured = w;
                ImageSource capIcon = ico;
                bool isFg = w.IsForeground;
                IconStyleFactory.ApplyVisualState(tile, _palette, false, isFg);
                tile.MouseEnter += delegate
                {
                    IconStyleFactory.ApplyVisualState(tile, _palette, true, isFg);
                    if (!_cfg.DockMagnification)
                        AnimateTileTransform(tile, 1.12, 0, 120);
                    if (_cfg.LiveWindowPreviews)
                    {
                        _tooltip.HideTip();
                        _livePreview.ShowPreview(captured.Handle, captured.Title, capIcon, tile, this, _cfg.IsTop, _palette);
                    }
                    else if (_cfg.NamesOnHover)
                    {
                        _tooltip.ShowTip(captured.Title, tile, this, _cfg.IsTop, _palette);
                    }
                };
                tile.MouseLeave += delegate
                {
                    IconStyleFactory.ApplyVisualState(tile, _palette, false, isFg);
                    if (!_cfg.DockMagnification)
                        AnimateTileTransform(tile, 1.0, 0, 140);
                    _tooltip.HideTip();
                    if (_cfg.LiveWindowPreviews)
                        _livePreview.ScheduleHide();
                };
                tile.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    _livePreview.HideImmediate();
                    ToggleFocusWindow(captured.Handle);
                };
                tile.MouseDown += delegate(object s, MouseButtonEventArgs e)
                {
                    if (e.ChangedButton == MouseButton.Middle)
                    {
                        e.Handled = true;
                        _livePreview.HideImmediate();
                        CloseWindowFast(captured.Handle);
                    }
                };
                tile.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    _livePreview.HideImmediate();
                    ShowOpenWindowMenu(captured);
                };

                _windowsPanel.Children.Add(tile);
                _magnifyTiles.Add(tile);
                shown++;
            }

            _winDivider.Visibility = (shown > 0) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CloseWindowFast(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            _livePreview.HideImmediate();
            _tooltip.HideTip();

            // Instant optimistic UI removal (0ms perceived close latency!)
            _closingWindows[hwnd] = DateTime.UtcNow.AddMilliseconds(1800);
            _lastWindowSignature = "";
            RefreshOpenWindowsIfChanged();

            // Send native close message asynchronously so the dock thread never blocks
            NativeWin32.CloseWindowGracefullyAsync(hwnd);
        }

        private void RefreshOpenWindowsIfChanged()
        {
            if (!_cfg.ShowOpenWindows) return;

            // Prune expired optimistic-close entries
            if (_closingWindows.Count > 0)
            {
                DateTime now = DateTime.UtcNow;
                List<IntPtr> expired = null;
                foreach (KeyValuePair<IntPtr, DateTime> kv in _closingWindows)
                {
                    if (now >= kv.Value || !NativeWin32.IsWindow(kv.Key))
                    {
                        if (expired == null) expired = new List<IntPtr>();
                        expired.Add(kv.Key);
                    }
                }
                if (expired != null)
                {
                    for (int i = 0; i < expired.Count; i++)
                        _closingWindows.Remove(expired[i]);
                }
            }

            List<OpenWindowInfo> wins = ReadOpenWindows();
            string sig = "";
            for (int i = 0; i < wins.Count; i++)
                sig += wins[i].Handle.ToInt64() + (wins[i].IsForeground ? "*" : "") + "|";
            if (sig != _lastWindowSignature)
            {
                _lastWindowSignature = sig;
                RebuildTaskbarAppPanels(wins);
            }
        }

        private List<OpenWindowInfo> ReadOpenWindows()
        {
            List<OpenWindowInfo> list = new List<OpenWindowInfo>();
            uint ownPid = (uint)Process.GetCurrentProcess().Id;
            IntPtr fg = NativeWin32.GetForegroundWindow();

            NativeWin32.EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (_closingWindows.ContainsKey(hWnd)) return true;
                if (!NativeWin32.IsWindowVisible(hWnd)) return true;
                if (NativeWin32.IsWindowCloaked(hWnd)) return true;
                if (NativeWin32.GetWindow(hWnd, NativeWin32.GW_OWNER) != IntPtr.Zero) return true;

                int ex = NativeWin32.GetWindowLong(hWnd, NativeWin32.GWL_EXSTYLE);
                if ((ex & NativeWin32.WS_EX_TOOLWINDOW) != 0) return true;

                string title = NativeWin32.GetWindowTitle(hWnd);
                if (string.IsNullOrEmpty(title)) return true;
                if (title == "Program Manager" || title == "Windows Input Experience") return true;

                uint pid;
                NativeWin32.GetWindowThreadProcessId(hWnd, out pid);
                if (pid == ownPid) return true;

                string pName = "";
                string exePath = "";
                string aumid = null;
                try
                {
                    using (Process pr = Process.GetProcessById((int)pid))
                    {
                        pName = pr.ProcessName;
                        try { exePath = pr.MainModule.FileName; } catch { }
                    }
                }
                catch { }

                string resolvedExe;
                aumid = NativeWin32.TryGetWindowAppUserModelId(hWnd, out resolvedExe);
                if (!string.IsNullOrEmpty(resolvedExe))
                    exePath = resolvedExe;
                else if (string.IsNullOrEmpty(exePath))
                    exePath = NativeWin32.TryGetProcessExePath(pid);

                list.Add(new OpenWindowInfo
                {
                    Handle = hWnd,
                    ProcessId = pid,
                    Title = title,
                    ProcessName = pName,
                    ExePath = exePath,
                    AppUserModelId = aumid,
                    IsForeground = (hWnd == fg)
                });
                return true;
            }, IntPtr.Zero);

            return list;
        }

        private static List<OpenWindowInfo> GetMatchedWindowsForPinned(PinnedAppItem app, List<OpenWindowInfo> wins)
        {
            List<OpenWindowInfo> res = new List<OpenWindowInfo>();
            if (app == null || string.IsNullOrEmpty(app.Path) || wins == null) return res;

            string pinnedPath = app.Path.Trim();
            bool isAppsFolder = pinnedPath.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase);
            string pinnedAumid = isAppsFolder ? pinnedPath.Substring("shell:AppsFolder\\".Length).Trim() : null;
            string exeName = isAppsFolder ? "" : Path.GetFileNameWithoutExtension(pinnedPath);
            if (!isAppsFolder && string.IsNullOrEmpty(exeName)) return res;
            bool isExplorer = string.Equals(exeName, "explorer", StringComparison.OrdinalIgnoreCase);
            bool isWt = string.Equals(exeName, "wt", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < wins.Count; i++)
            {
                OpenWindowInfo w = wins[i];
                if (isAppsFolder)
                {
                    if (!string.IsNullOrEmpty(w.AppUserModelId) &&
                        string.Equals(w.AppUserModelId, pinnedAumid, StringComparison.OrdinalIgnoreCase))
                        res.Add(w);
                    continue;
                }
                if (isExplorer)
                {
                    if (string.Equals(w.ProcessName, "explorer", StringComparison.OrdinalIgnoreCase))
                    {
                        string cls = NativeWin32.GetWindowClassName(w.Handle);
                        if (cls == "CabinetWClass" || cls == "ExploreWClass")
                            res.Add(w);
                    }
                    continue;
                }

                bool match = false;
                if (!string.IsNullOrEmpty(w.ExePath) && string.Equals(w.ExePath, pinnedPath, StringComparison.OrdinalIgnoreCase))
                    match = true;
                else if (string.Equals(w.ProcessName, exeName, StringComparison.OrdinalIgnoreCase))
                    match = true;
                else if (isWt && string.Equals(w.ProcessName, "WindowsTerminal", StringComparison.OrdinalIgnoreCase))
                    match = true;

                if (match) res.Add(w);
            }
            return res;
        }

        private static void LaunchNewAppInstance(PinnedAppItem app)
        {
            try
            {
                if (app != null && !string.IsNullOrEmpty(app.Path))
                {
                    if (app.Path.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
                        Process.Start(new ProcessStartInfo("explorer.exe", app.Path) { UseShellExecute = true });
                    else
                        Process.Start(new ProcessStartInfo(app.Path) { UseShellExecute = true });
                }
            }
            catch { }
        }

        private void LaunchOrFocusApp(PinnedAppItem app)
        {
            try
            {
                List<OpenWindowInfo> matched = GetMatchedWindowsForPinned(app, ReadOpenWindows());
                if (matched.Count > 0)
                {
                    ToggleFocusWindow(matched[0].Handle);
                    return;
                }
                LaunchNewAppInstance(app);
            }
            catch { }
        }

        private static void ToggleFocusWindow(IntPtr hwnd)
        {
            if (!NativeWin32.IsWindow(hwnd)) return;
            if (NativeWin32.IsIconic(hwnd))
            {
                NativeWin32.ShowWindow(hwnd, NativeWin32.SW_RESTORE);
                NativeWin32.SetForegroundWindow(hwnd);
            }
            else if (NativeWin32.GetForegroundWindow() == hwnd)
            {
                NativeWin32.ShowWindow(hwnd, NativeWin32.SW_MINIMIZE);
            }
            else
            {
                NativeWin32.SetForegroundWindow(hwnd);
            }
        }

        private ImageSource GetCachedIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            ImageSource cached;
            if (_iconCache.TryGetValue(path, out cached)) return cached;
            ImageSource img = NativeWin32.ExtractIconFromPath(path);
            _iconCache[path] = img;
            return img;
        }

        // ====== TIMER UPDATES ======
        private void OnFastTick(object sender, EventArgs e)
        {
            _tel.UpdateFastMetrics(_cfg);
            _tel.PollClipboardSta();

            // Remember last non-Monolith foreground window for 1-click Window Snapping
            try
            {
                IntPtr fg = NativeWin32.GetForegroundWindow();
                if (fg != IntPtr.Zero)
                {
                    uint pid;
                    NativeWin32.GetWindowThreadProcessId(fg, out pid);
                    if (pid != (uint)Process.GetCurrentProcess().Id)
                    {
                        string cls = NativeWin32.GetWindowClassName(fg);
                        if (cls != "Progman" && cls != "WorkerW" && cls != "Shell_TrayWnd")
                            _tel.LastActiveExternalWindow = fg;
                    }
                }
            }
            catch { }

            // Refresh every active widget card
            for (int i = 0; i < _widgetHandles.Count; i++)
            {
                if (_widgetHandles[i].RefreshAction != null)
                    _widgetHandles[i].RefreshAction();
            }

            // Refresh open windows via fast helper
            RefreshOpenWindowsIfChanged();

            // Fullscreen check
            if (_cfg.HideOnFullscreen)
                CheckFullscreen();
            else if (_fullscreenHidden)
            {
                _fullscreenHidden = false;
                ApplyVisibility();
            }

            // Keep native taskbar hidden if user selected it (zero work-area bouncing!)
            if (_cfg.HideWindowsTaskbar && !_exiting && !_trayFlyoutActive && !_tempNativeTaskbarShown)
            {
                NativeWin32.HideNativeTaskbars();
            }
        }

        private void OnAutoHidePoll(object sender, EventArgs e)
        {
            // Keep legacy Shell_TrayWnd 100% invisible (alpha=0) AND keep the Hidden Icons flyout
            // anchored directly above/below Monolith's own '^' button while open.
            if (_trayFlyoutActive)
            {
                if (_cfg.HideWindowsTaskbar && !_tempNativeTaskbarShown)
                    NativeWin32.KeepTaskbarGhostedIfVisible();

                NativeWin32.RepositionHiddenIconsFlyout(_trayAnchorX, _trayDockTopPx, _trayDockBottomPx, _cfg.IsTop);

                double elapsedMs = (DateTime.UtcNow - _trayFlyoutStartedUtc).TotalMilliseconds;
                if (elapsedMs < 1400)
                    NativeWin32.SuppressExplorerTaskbarTooltips(IntPtr.Zero, true);
                if (!_trayFlyoutOpening && elapsedMs > 550)
                {
                    bool flyoutOpen = NativeWin32.IsHiddenIconsFlyoutOpen();
                    if (!flyoutOpen)
                    {
                        _trayFlyoutActive = false;
                        UpdateTrayChevronVisual(false);
                        if (_cfg.HideWindowsTaskbar && !_tempNativeTaskbarShown && !_exiting)
                            NativeWin32.HideNativeTaskbars();
                    }
                }
            }

            // Auto-dodge when Windows 11's native SearchHost (Win+S) is focused so it never slices Monolith!
            bool nativeSearchOpen = IsNativeSearchHostFocused();
            if (nativeSearchOpen != _searchHostHidden)
            {
                _searchHostHidden = nativeSearchOpen;
                ApplyVisibility();
            }

            // Smart Intellihide: automatically tuck away ONLY when a window actually overlaps Monolith's region
            bool smartOverlap = !_cfg.FullWidth && !_cfg.AutoHide && (_cfg.DockOverlapMode == "SmartHide") && IsForegroundWindowOverlappingDock();
            if (smartOverlap != _smartOverlapActive)
            {
                _smartOverlapActive = smartOverlap;
                if (!smartOverlap) _autoHidePeeking = true;
                ApplyVisibility();
            }

            bool needPeek = _cfg.AutoHide || _smartOverlapActive;
            bool pinnedOpen = _editMode || _activePopover != null || _controlCenterWin != null || _spotlightWin != null || _pickerWin != null || _settingsWin != null || _trayFlyoutActive;
            if (!needPeek || pinnedOpen)
            {
                if (!_autoHidePeeking)
                {
                    _autoHidePeeking = true;
                    ApplyVisibility();
                }
                return;
            }

            NativeWin32.POINT pt;
            if (!NativeWin32.GetCursorPos(out pt)) return;

            PresentationSource src = PresentationSource.FromVisual(this);
            double dpiY = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformToDevice.M22 : 1.0;
            double cy = pt.Y / dpiY;
            double sh = SystemParameters.PrimaryScreenHeight;

            bool nearEdge = _cfg.IsTop ? (cy <= 10) : (cy >= sh - 10);
            bool insideDock = _autoHidePeeking && (cy >= Top && cy <= Top + ActualHeight);

            if (nearEdge || insideDock)
            {
                _autoHideCooldown = 6; // stay visible briefly after leaving
                if (!_autoHidePeeking)
                {
                    _autoHidePeeking = true;
                    ApplyVisibility();
                }
            }
            else if (_autoHideCooldown > 0)
            {
                _autoHideCooldown--;
            }
            else if (_autoHidePeeking)
            {
                _autoHidePeeking = false;
                ApplyVisibility();
            }
        }

        private bool IsForegroundWindowOverlappingDock()
        {
            try
            {
                IntPtr fg = NativeWin32.GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;
                IntPtr ownHwnd = new WindowInteropHelper(this).Handle;
                if (fg == ownHwnd) return false;

                uint pid;
                NativeWin32.GetWindowThreadProcessId(fg, out pid);
                if (pid == (uint)Process.GetCurrentProcess().Id) return false;

                string cls = NativeWin32.GetWindowClassName(fg);
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                    return false;

                int ex = NativeWin32.GetWindowLong(fg, NativeWin32.GWL_EXSTYLE);
                if ((ex & NativeWin32.WS_EX_TOOLWINDOW) != 0) return false;

                try
                {
                    using (Process pr = Process.GetProcessById((int)pid))
                    {
                        if (IsCaptureOrShellOverlayProcess(pr.ProcessName)) return false;
                    }
                }
                catch { }

                NativeWin32.RECT rc;
                if (!NativeWin32.GetWindowRect(fg, out rc)) return false;

                PresentationSource src = PresentationSource.FromVisual(this);
                double dpiX = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformToDevice.M11 : 1.0;
                double dpiY = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformToDevice.M22 : 1.0;
                int sw = NativeWin32.GetSystemMetrics(0);
                int sh = NativeWin32.GetSystemMetrics(1);

                int dockFootprintH = (int)Math.Round((_metrics.BarHeight + _metrics.EdgeMargin) * dpiY);
                bool vertOverlap = _cfg.IsTop ? (rc.Top < dockFootprintH) : (rc.Bottom > sh - dockFootprintH);
                if (!vertOverlap) return false;

                double dockW = _backdropBorder != null && _backdropBorder.ActualWidth > 100
                    ? _backdropBorder.ActualWidth * dpiX
                    : sw * 0.45;
                int dockLeft = (int)Math.Round((sw - dockW) / 2.0);
                int dockRight = (int)Math.Round(dockLeft + dockW);
                return rc.Right > dockLeft && rc.Left < dockRight;
            }
            catch { return false; }
        }

        private static bool IsCaptureOrShellOverlayProcess(string procName)
        {
            if (string.IsNullOrEmpty(procName)) return false;
            return string.Equals(procName, "ScreenClippingHost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "SnippingTool", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "ShellExperienceHost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "SearchHost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "LockApp", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "ShareX", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "Lightshot", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "Greenshot", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "Flameshot", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "PicPick", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "Gyazo", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(procName, "explorer", StringComparison.OrdinalIgnoreCase);
        }

        private void CheckFullscreen()
        {
            bool fs = false;
            try
            {
                int quns = 0;
                if (NativeWin32.SHQueryUserNotificationState(out quns) == 0 && quns == NativeWin32.QUNS_RUNNING_D3D_FULL_SCREEN)
                {
                    fs = true;
                }
                else
                {
                    IntPtr fg = NativeWin32.GetForegroundWindow();
                    if (fg != IntPtr.Zero && fg != new WindowInteropHelper(this).Handle)
                    {
                        string cls = NativeWin32.GetWindowClassName(fg);
                        if (cls != "Progman" && cls != "WorkerW" && cls != "Shell_TrayWnd")
                        {
                            int exStyle = NativeWin32.GetWindowLong(fg, NativeWin32.GWL_EXSTYLE);
                            bool isToolOverlay = (exStyle & NativeWin32.WS_EX_TOOLWINDOW) != 0;

                            if (!isToolOverlay)
                            {
                                string pName = "";
                                try
                                {
                                    uint pid;
                                    NativeWin32.GetWindowThreadProcessId(fg, out pid);
                                    if (pid != 0)
                                    {
                                        using (Process pr = Process.GetProcessById((int)pid))
                                            pName = pr.ProcessName;
                                    }
                                }
                                catch { }

                                if (!IsCaptureOrShellOverlayProcess(pName))
                                {
                                    // Normal maximized windows (Chrome, VS Code, Explorer, etc.) have WS_CAPTION + (WS_THICKFRAME or IsZoomed);
                                    // true fullscreen games & F11/YouTube fullscreen strip window borders & caption!
                                    int style = NativeWin32.GetWindowLong(fg, NativeWin32.GWL_STYLE);
                                    bool hasCaption = (style & NativeWin32.WS_CAPTION) == NativeWin32.WS_CAPTION;
                                    bool hasThickFrame = (style & NativeWin32.WS_THICKFRAME) != 0;
                                    bool isNormalMaximized = hasCaption && (hasThickFrame || NativeWin32.IsZoomed(fg));

                                    if (!isNormalMaximized)
                                    {
                                        NativeWin32.RECT rc;
                                        if (NativeWin32.GetWindowRect(fg, out rc))
                                        {
                                            int sw = NativeWin32.GetSystemMetrics(0);
                                            int sh = NativeWin32.GetSystemMetrics(1);
                                            if (rc.Left <= 0 && rc.Top <= 0 && rc.Right >= sw && rc.Bottom >= sh)
                                                fs = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            if (fs != _fullscreenHidden)
            {
                _fullscreenHidden = fs;
                ApplyVisibility();
            }
        }

        private static bool IsNativeSearchHostFocused()
        {
            try
            {
                IntPtr fg = NativeWin32.GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;
                uint pid;
                NativeWin32.GetWindowThreadProcessId(fg, out pid);
                if (pid == 0) return false;
                using (Process pr = Process.GetProcessById((int)pid))
                {
                    return string.Equals(pr.ProcessName, "SearchHost", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }

        private void ApplyVisibility()
        {
            bool needPeek = _cfg.AutoHide || _smartOverlapActive;
            bool shouldShow = !_userHidden && !_fullscreenHidden && !_searchHostHidden && (!needPeek || _autoHidePeeking);
            Visibility = shouldShow ? Visibility.Visible : Visibility.Hidden;
            if (_mediaBackdrop != null) _mediaBackdrop.SetPaused(!shouldShow);
            if (!shouldShow) _tooltip.HideTip();
        }

        // ====== POSITIONING & OPTICAL GLASS BACKDROP ======
        private void PositionDock()
        {
            double sw = SystemParameters.PrimaryScreenWidth;
            double sh = SystemParameters.PrimaryScreenHeight;
            double margin = _cfg.FullWidth ? 0 : _metrics.EdgeMargin;

            double w = ActualWidth > 0 ? ActualWidth : _metrics.BarHeight * 4;
            double h = ActualHeight > 0 ? ActualHeight : _metrics.BarHeight + 26;

            Left = _cfg.FullWidth ? 0 : Math.Round((sw - w) / 2.0);
            if (_cfg.IsTop)
                Top = _cfg.FullWidth ? 0 : margin - 12;
            else
                Top = _cfg.FullWidth ? (sh - h) : (sh - h - margin + 14);
        }

        private void UpdateBackdrop(bool force)
        {
            try
            {
                PresentationSource src = PresentationSource.FromVisual(this);
                double dpiX = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformToDevice.M11 : 1.0;
                double dpiY = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformToDevice.M22 : 1.0;

                double padL = _shadowHost.Margin.Left;
                double padT = _shadowHost.Margin.Top;
                double bW = _backdropBorder.ActualWidth > 10 ? _backdropBorder.ActualWidth : ActualWidth - padL - _shadowHost.Margin.Right;
                double bH = _metrics.BarHeight;
                double bL = Left + padL;
                double bT = Top + padT;
                double rad = _cfg.FullWidth ? 0 : _metrics.OuterRadius;

                string sig = string.Format("{0}|{1:0}|{2:0}|{3:0}|{4:0}|{5:0.00}|{6}|{7}|{8:0.00}|{9}|{10}",
                    _palette.Name, bL, bT, bW, bH, _cfg.GlassIntensity, _cfg.ClearCardStyle,
                    _cfg.DockTintColor, _cfg.ColorTintStrength, _cfg.Accent, _cfg.CustomAccentHex);

                if (!force && sig == _lastBackdropSignature) return;
                _lastBackdropSignature = sig;

                _backdropBorder.Background = LiquidGlassRenderer.RenderBackdropBrush(bL, bT, bW, bH, rad, dpiX, dpiY, _palette);
            }
            catch { }
        }

        // ====== MODALS, POPOVERS & MENUS ======
        private void OpenPopoverForWidget(string widgetId, FrameworkElement anchor)
        {
            if (_editMode) return;
            _tooltip.HideTip();
            _livePreview.HideImmediate();

            if (widgetId == "spotlight")
            {
                OpenSpotlight();
                return;
            }
            if (widgetId == "control_center")
            {
                OpenControlCenter(anchor);
                return;
            }

            if (_activePopover != null)
            {
                try { _activePopover.Close(); } catch { }
                _activePopover = null;
            }
            _activePopover = new PopoverWindow(widgetId, anchor, this, _cfg, _palette, _tel, delegate
            {
                for (int i = 0; i < _widgetHandles.Count; i++)
                    if (_widgetHandles[i].RefreshAction != null) _widgetHandles[i].RefreshAction();
            });
            _activePopover.Closed += delegate { _activePopover = null; };
            _activePopover.Show();
        }

        private void OpenSpotlight()
        {
            _tooltip.HideTip();
            _livePreview.HideImmediate();
            if (_spotlightWin != null)
            {
                try { _spotlightWin.Close(); } catch { }
                _spotlightWin = null;
                return;
            }
            // Prevent immediate reopen when user clicks the Search icon while Spotlight was open (Deactivated closes it first)
            if ((DateTime.UtcNow - _spotlightClosedUtc).TotalMilliseconds < 260)
                return;

            _spotlightWin = new SpotlightWindow(this, _cfg, _palette, OpenWidgetPicker, OpenSettings);
            _spotlightWin.Closed += delegate
            {
                _spotlightWin = null;
                _spotlightClosedUtc = DateTime.UtcNow;
            };
            _spotlightWin.Show();
            _spotlightWin.Activate();
        }

        private void OpenControlCenter(FrameworkElement anchor)
        {
            _tooltip.HideTip();
            _livePreview.HideImmediate();
            if (_controlCenterWin != null)
            {
                try { _controlCenterWin.Close(); } catch { }
                _controlCenterWin = null;
                return;
            }
            _controlCenterWin = new ControlCenterWindow(anchor ?? _backdropBorder, this, _cfg, _palette, _tel, delegate
            {
                for (int i = 0; i < _widgetHandles.Count; i++)
                    if (_widgetHandles[i].RefreshAction != null) _widgetHandles[i].RefreshAction();
            }, OpenSettings);
            _controlCenterWin.Closed += delegate { _controlCenterWin = null; };
            _controlCenterWin.Show();
            _controlCenterWin.Activate();
        }

        private void OpenWidgetPicker()
        {
            if (_pickerWin != null)
            {
                _pickerWin.Activate();
                return;
            }
            _editMode = true;
            SetEditBadgesVisible(true);

            _pickerWin = new WidgetPickerWindow(_cfg, _palette, _tel, delegate { RebuildDockItems(); });
            _pickerWin.Closed += delegate
            {
                _pickerWin = null;
                _editMode = false;
                SetEditBadgesVisible(false);
            };
            _pickerWin.Show();
            _pickerWin.Activate();
        }

        private void SetEditBadgesVisible(bool show)
        {
            for (int i = 0; i < _widgetHandles.Count; i++)
            {
                if (_widgetHandles[i].RemoveBadge != null)
                    _widgetHandles[i].RemoveBadge.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void RemoveWidgetFromDock(string id)
        {
            _cfg.ActiveWidgets.Remove(id);
            ConfigStore.Save(_cfg);
            RebuildDockItems();
            if (_pickerWin != null) _pickerWin.RefreshGallery();
        }

        private void ComputeTrayAnchorPixels()
        {
            try
            {
                FrameworkElement anchorEl = (_trayChevronBtn != null) ? (FrameworkElement)_trayChevronBtn : (FrameworkElement)_backdropBorder;
                Point btnPt = anchorEl.PointToScreen(new Point(anchorEl.ActualWidth / 2.0, 0));
                Point dockTopPt = _backdropBorder.PointToScreen(new Point(0, 0));
                Point dockBotPt = _backdropBorder.PointToScreen(new Point(0, _backdropBorder.ActualHeight));

                _trayAnchorX = (int)Math.Round(btnPt.X);
                _trayDockTopPx = (int)Math.Round(dockTopPt.Y);
                _trayDockBottomPx = (int)Math.Round(dockBotPt.Y);
            }
            catch
            {
                _trayAnchorX = NativeWin32.GetSystemMetrics(0) / 2;
                _trayDockTopPx = NativeWin32.GetSystemMetrics(1) - 80;
                _trayDockBottomPx = 74;
            }
        }

        private void ToggleHiddenIconsTray()
        {
            _tooltip.HideTip();
            ComputeTrayAnchorPixels();

            if (_trayFlyoutOpening) return;

            if (_trayFlyoutActive && NativeWin32.IsHiddenIconsFlyoutOpen())
            {
                _trayFlyoutActive = false;
                UpdateTrayChevronVisual(false);
                NativeWin32.TriggerHiddenIconsFlyoutAsync(_trayAnchorX, _trayDockTopPx, _trayDockBottomPx, _cfg.IsTop, delegate
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (_cfg.HideWindowsTaskbar && !_tempNativeTaskbarShown && !_exiting)
                            NativeWin32.HideNativeTaskbars();
                    }));
                });
                return;
            }

            _trayFlyoutActive = true;
            _trayFlyoutOpening = true;
            _trayFlyoutStartedUtc = DateTime.UtcNow;
            UpdateTrayChevronVisual(true);

            NativeWin32.TriggerHiddenIconsFlyoutAsync(_trayAnchorX, _trayDockTopPx, _trayDockBottomPx, _cfg.IsTop, delegate(bool opened)
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _trayFlyoutOpening = false;
                    _trayFlyoutStartedUtc = DateTime.UtcNow;
                    if (!opened)
                    {
                        _trayFlyoutActive = false;
                        UpdateTrayChevronVisual(false);
                        if (_cfg.HideWindowsTaskbar && !_tempNativeTaskbarShown && !_exiting)
                            NativeWin32.HideNativeTaskbars();
                    }
                }));
            });
        }

        private void UpdateTrayChevronVisual(bool active)
        {
            if (_trayChevronBtn == null) return;
            if (active)
            {
                _trayChevronBtn.Background = ThemeManager.Brush(_palette.Accent);
                _trayChevronBtn.BorderBrush = ThemeManager.CreateOrbRimBrush(_palette, true);
            }
            else
            {
                IconStyleFactory.ApplyVisualState(_trayChevronBtn, _palette, false, false);
            }
        }

        private void ShowTrayChevronMenu()
        {
            List<GlassMenuItemDef> items = new List<GlassMenuItemDef>();
            items.Add(new GlassMenuItemDef { IsHeader = true, Label = L10n.T(_cfg, "Sistem Tepsisi", "System Tray") });
            items.Add(new GlassMenuItemDef
            {
                Label = L10n.T(_cfg, "Gizli Simgeleri Göster", "Show Hidden Icons"),
                Glyph = "\uE70E",
                OnClick = ToggleHiddenIconsTray
            });
            items.Add(new GlassMenuItemDef
            {
                Label = L10n.T(_cfg, "Hızlı Ayarlar (Wi-Fi / Ses)", "Quick Settings (Win+A)"),
                Glyph = "\uE701",
                RightHint = "Win+A",
                OnClick = delegate
                {
                    NativeWin32.SendWinCombo(NativeWin32.VK_A);
                }
            });
            items.Add(new GlassMenuItemDef
            {
                Label = L10n.T(_cfg, "Bildirim Merkezi", "Notification Center"),
                Glyph = "\uEA8F",
                RightHint = "Win+N",
                OnClick = delegate
                {
                    NativeWin32.SendWinCombo(NativeWin32.VK_N);
                }
            });
            items.Add(new GlassMenuItemDef { IsSeparator = true });
            items.Add(new GlassMenuItemDef
            {
                Label = _tempNativeTaskbarShown
                    ? L10n.T(_cfg, "Windows Görev Çubuğunu Tekrar Gizle", "Hide Windows Taskbar Again")
                    : L10n.T(_cfg, "Windows Görev Çubuğunu Geçici Göster", "Temporarily Show Windows Taskbar"),
                Glyph = "\uE74C",
                IsChecked = _tempNativeTaskbarShown,
                OnClick = delegate
                {
                    _tempNativeTaskbarShown = !_tempNativeTaskbarShown;
                    if (_tempNativeTaskbarShown) NativeWin32.RestoreNativeTaskbars();
                    else if (_cfg.HideWindowsTaskbar)
                    {
                        NativeWin32.HideNativeTaskbars();
                        SyncDesktopWorkArea();
                    }
                }
            });
            items.Add(new GlassMenuItemDef
            {
                Label = L10n.T(_cfg, "Sistem Tepsisi Ayarları", "System Tray Settings"),
                Glyph = "\uE713",
                OnClick = delegate { try { Process.Start(new ProcessStartInfo("ms-settings:taskbar") { UseShellExecute = true }); } catch { } }
            });

            new GlassMenuWindow(items, _palette, this, _cfg.IsTop).Show();
        }

        public void UpdateMediaBackdropLive()
        {
            if (_mediaBackdrop != null)
            {
                _mediaBackdrop.Opacity = _cfg.DockMediaOpacity;
                _mediaBackdrop.UpdateRegion(_cfg.DockMediaOffsetX, _cfg.DockMediaOffsetY, _cfg.DockMediaZoom);
            }
        }

        public double GetDockAspectRatio()
        {
            if (_backdropBorder != null && _backdropBorder.ActualWidth > 120 && _backdropBorder.ActualHeight > 20)
                return _backdropBorder.ActualWidth / _backdropBorder.ActualHeight;
            return 9.5;
        }

        private void OpenSettings()
        {
            if (_settingsWin != null)
            {
                _settingsWin.Activate();
                return;
            }
            _settingsWin = new SettingsWindow(_cfg, _tel, delegate { RebuildDockItems(); }, ExitAndRestore, UpdateMediaBackdropLive, GetDockAspectRatio(), OpenWidgetPicker);
            _settingsWin.Closed += delegate { _settingsWin = null; };
            _settingsWin.Show();
            _settingsWin.Activate();
        }

        private void ShowToast(string title, string message)
        {
            try
            {
                GlassToastWindow toast = new GlassToastWindow(title, message, this, _cfg.IsTop, _palette);
                toast.Show();
            }
            catch { }
        }

        private void OnDockFileDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files == null || files.Length == 0) return;

                foreach (string f in files)
                {
                    if (File.Exists(f) || Directory.Exists(f))
                    {
                        string name = Path.GetFileNameWithoutExtension(f);
                        if (string.IsNullOrEmpty(name)) name = Path.GetFileName(f);
                        _cfg.PinnedApps.Add(new PinnedAppItem
                        {
                            Name = name,
                            Path = f,
                            IconPath = f,
                            Glyph = "\uE71D"
                        });
                    }
                }
                ConfigStore.Save(_cfg);
                RebuildDockItems();
                ShowToast("Monolith", L10n.T(_cfg, "Uygulama dock'a sabitlendi!", "Pinned to dock!"));
            }
            catch { }
        }

        private void ShowPinnedAppMenu(PinnedAppItem app, int idx, List<OpenWindowInfo> matchedWins)
        {
            List<GlassMenuItemDef> items = new List<GlassMenuItemDef>();
            items.Add(new GlassMenuItemDef { IsHeader = true, Label = app.Name });

            if (matchedWins != null && matchedWins.Count > 0)
            {
                for (int i = 0; i < Math.Min(4, matchedWins.Count); i++)
                {
                    OpenWindowInfo w = matchedWins[i];
                    string wLabel = w.Title.Length > 34 ? (w.Title.Substring(0, 32) + "...") : w.Title;
                    items.Add(new GlassMenuItemDef
                    {
                        Label = wLabel,
                        Glyph = "\uE8A7",
                        IsChecked = w.IsForeground,
                        OnClick = delegate { ToggleFocusWindow(w.Handle); }
                    });
                }
                items.Add(new GlassMenuItemDef
                {
                    Label = L10n.T(_cfg, "Yeni Pencere Aç", "Open New Window"),
                    Glyph = "\uE710",
                    OnClick = delegate { LaunchNewAppInstance(app); }
                });
                items.Add(new GlassMenuItemDef
                {
                    Label = matchedWins.Count > 1
                        ? L10n.T(_cfg, "Tüm Pencereleri Kapat (" + matchedWins.Count + ")", "Close All Windows (" + matchedWins.Count + ")")
                        : L10n.T(_cfg, "Pencereyi Kapat", "Close Window"),
                    Glyph = "\uE711",
                    IsDanger = true,
                    OnClick = delegate
                    {
                        for (int k = 0; k < matchedWins.Count; k++)
                            CloseWindowFast(matchedWins[k].Handle);
                    }
                });
            }
            else
            {
                items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Aç", "Open"), Glyph = "\uE8A7", OnClick = delegate { LaunchNewAppInstance(app); } });
            }

            items.Add(new GlassMenuItemDef { IsSeparator = true });
            if (idx > 0)
            {
                items.Add(new GlassMenuItemDef
                {
                    Label = L10n.T(_cfg, "Sola Taşı", "Move Left"),
                    Glyph = "\uE72B",
                    OnClick = delegate
                    {
                        PinnedAppItem tmp = _cfg.PinnedApps[idx - 1];
                        _cfg.PinnedApps[idx - 1] = _cfg.PinnedApps[idx];
                        _cfg.PinnedApps[idx] = tmp;
                        ConfigStore.Save(_cfg);
                        RebuildDockItems();
                    }
                });
            }
            if (idx < _cfg.PinnedApps.Count - 1)
            {
                items.Add(new GlassMenuItemDef
                {
                    Label = L10n.T(_cfg, "Sağa Taşı", "Move Right"),
                    Glyph = "\uE72A",
                    OnClick = delegate
                    {
                        PinnedAppItem tmp = _cfg.PinnedApps[idx + 1];
                        _cfg.PinnedApps[idx + 1] = _cfg.PinnedApps[idx];
                        _cfg.PinnedApps[idx] = tmp;
                        ConfigStore.Save(_cfg);
                        RebuildDockItems();
                    }
                });
            }
            items.Add(new GlassMenuItemDef
            {
                Label = L10n.T(_cfg, "Dock'tan Kaldır", "Unpin from dock"),
                Glyph = "\uE77A",
                IsDanger = true,
                OnClick = delegate
                {
                    _cfg.PinnedApps.RemoveAt(idx);
                    ConfigStore.Save(_cfg);
                    RebuildDockItems();
                }
            });
            new GlassMenuWindow(items, _palette, this, _cfg.IsTop).Show();
        }

        private void ShowOpenWindowMenu(OpenWindowInfo w)
        {
            List<GlassMenuItemDef> items = new List<GlassMenuItemDef>();
            items.Add(new GlassMenuItemDef { IsHeader = true, Label = w.Title });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Pencereyi Öne Getir", "Focus window"), Glyph = "\uE8A7", OnClick = delegate { ToggleFocusWindow(w.Handle); } });
            string pinTarget = !string.IsNullOrEmpty(w.AppUserModelId)
                ? ("shell:AppsFolder\\" + w.AppUserModelId)
                : w.ExePath;
            string pinName = !string.IsNullOrEmpty(w.AppUserModelId) && !string.IsNullOrEmpty(w.Title)
                ? w.Title
                : SystemTelemetry.CleanAppName(w.ProcessName);
            if (!string.IsNullOrEmpty(pinTarget))
            {
                items.Add(new GlassMenuItemDef
                {
                    Label = L10n.T(_cfg, "Dock'a Sabitle", "Pin to dock"),
                    Glyph = "\uE718",
                    OnClick = delegate
                    {
                        _cfg.PinnedApps.Add(new PinnedAppItem
                        {
                            Name = pinName,
                            Path = pinTarget,
                            IconPath = pinTarget,
                            Glyph = "\uE71D"
                        });
                        ConfigStore.Save(_cfg);
                        RebuildDockItems();
                    }
                });
            }
            items.Add(new GlassMenuItemDef { IsSeparator = true });
            items.Add(new GlassMenuItemDef
            {
                Label = L10n.T(_cfg, "Pencereyi Kapat", "Close window"),
                Glyph = "\uE8BB",
                OnClick = delegate { CloseWindowFast(w.Handle); }
            });
            items.Add(new GlassMenuItemDef
            {
                Label = L10n.T(_cfg, "Görevi Sonlandır", "End task"),
                Glyph = "\uE711",
                IsDanger = true,
                OnClick = delegate { try { Process.GetProcessById((int)w.ProcessId).Kill(); } catch { } }
            });
            new GlassMenuWindow(items, _palette, this, _cfg.IsTop).Show();
        }

        private void ShowDockContextMenu()
        {
            List<GlassMenuItemDef> items = new List<GlassMenuItemDef>();
            items.Add(new GlassMenuItemDef { IsHeader = true, Label = "Monolith  \u2022  " + _cfg.Theme });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Tüm Widget'lar...", "All Widgets..."), Glyph = "\uEA86", RightHint = "Ctrl+Shift+W", OnClick = OpenWidgetPicker });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Ayarlar...", "Settings..."), Glyph = "\uE713", RightHint = "Ctrl+Shift+S", OnClick = OpenSettings });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Gizli Simgeleri Göster", "Show Hidden Icons"), Glyph = "\uE70E", OnClick = ToggleHiddenIconsTray });
            items.Add(new GlassMenuItemDef { IsSeparator = true });

            for (int i = 0; i < ThemeManager.ThemeNames.Length; i++)
            {
                string t = ThemeManager.ThemeNames[i];
                items.Add(new GlassMenuItemDef
                {
                    Label = t,
                    Glyph = "\uE790",
                    IsChecked = (_cfg.Theme == t),
                    OnClick = delegate { _cfg.Theme = t; ConfigStore.Save(_cfg); RebuildDockItems(); }
                });
            }

            items.Add(new GlassMenuItemDef { IsSeparator = true });
            items.Add(new GlassMenuItemDef
            {
                Label = _cfg.IsTop ? L10n.T(_cfg, "Alt Kenara Taşı", "Move to bottom") : L10n.T(_cfg, "Üst Kenara Taşı", "Move to top"),
                Glyph = _cfg.IsTop ? "\uE74B" : "\uE74A",
                OnClick = delegate { _cfg.IsTop = !_cfg.IsTop; ConfigStore.Save(_cfg); RebuildDockItems(); }
            });
            items.Add(new GlassMenuItemDef
            {
                Label = _cfg.FullWidth ? L10n.T(_cfg, "Yüzen Kapsül Modu", "Floating Capsule") : L10n.T(_cfg, "Tam Genişlik Modu", "Full Width"),
                Glyph = "\uE740",
                OnClick = delegate { _cfg.FullWidth = !_cfg.FullWidth; ConfigStore.Save(_cfg); RebuildDockItems(); }
            });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Bildirim Merkezi", "Notifications"), Glyph = "\uE7E7", RightHint = "Win+N", OnClick = delegate { NativeWin32.OpenNotifications(); } });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Masaüstünü Göster", "Show Desktop"), Glyph = "\uE7F4", RightHint = "Win+D", OnClick = delegate { NativeWin32.ShowDesktop(); } });
            items.Add(new GlassMenuItemDef { Label = L10n.T(_cfg, "Görev Yöneticisi", "Task Manager"), Glyph = "\uE9D9", OnClick = delegate { try { Process.Start("taskmgr.exe"); } catch { } } });
            items.Add(new GlassMenuItemDef { IsSeparator = true });
            items.Add(new GlassMenuItemDef
            {
                Label = L10n.T(_cfg, "Görev Çubuğunu Geri Yükle & Çık", "Restore Taskbar & Exit"),
                Glyph = "\uE7E8",
                IsDanger = true,
                OnClick = ExitAndRestore
            });

            new GlassMenuWindow(items, _palette, this, _cfg.IsTop).Show();
        }

        private void SetupTrayIcon()
        {
            try
            {
                _trayIcon = new Forms.NotifyIcon();
                _trayIcon.Text = "Monolith v1.0";
                try
                {
                    _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Process.GetCurrentProcess().MainModule.FileName);
                }
                catch
                {
                    _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                }

                Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
                menu.Items.Add("Show / Hide Monolith", null, delegate
                {
                    Dispatcher.Invoke(new Action(delegate { _userHidden = !_userHidden; ApplyVisibility(); }));
                });
                menu.Items.Add("All Widgets...", null, delegate
                {
                    Dispatcher.Invoke(new Action(OpenWidgetPicker));
                });
                menu.Items.Add("Settings...", null, delegate
                {
                    Dispatcher.Invoke(new Action(OpenSettings));
                });
                menu.Items.Add("-");
                menu.Items.Add("Restore Taskbar & Exit", null, delegate
                {
                    Dispatcher.Invoke(new Action(ExitAndRestore));
                });
                _trayIcon.ContextMenuStrip = menu;
                _trayIcon.DoubleClick += delegate
                {
                    Dispatcher.Invoke(new Action(OpenSettings));
                };
                _trayIcon.Visible = true;
            }
            catch { }
        }

        public void ExitAndRestore()
        {
            _exiting = true;
            CleanupAndRestore();
            Application.Current.Shutdown();
        }

        private void CleanupAndRestore()
        {
            _exiting = true;
            try { _fastTimer.Stop(); } catch { }
            try { _windowPollTimer.Stop(); } catch { }
            try { _hoverPollTimer.Stop(); } catch { }
            try { if (_mediaBackdrop != null) _mediaBackdrop.ClearCurrent(); } catch { }
            try { ConfigStore.Save(_cfg); } catch { }
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    NativeWin32.UnregisterHotKey(hwnd, HOTKEY_TOGGLE);
                    NativeWin32.UnregisterHotKey(hwnd, HOTKEY_WIDGETS);
                    NativeWin32.UnregisterHotKey(hwnd, HOTKEY_SETTINGS);
                    NativeWin32.UnregisterHotKey(hwnd, HOTKEY_SPOTLIGHT);
                }
            }
            catch { }
            try { _tooltip.Close(); } catch { }
            try { _livePreview.Close(); } catch { }
            try { if (_trayIcon != null) { _trayIcon.Visible = false; _trayIcon.Dispose(); _trayIcon = null; } } catch { }
            NativeWin32.RestoreNativeTaskbars();
        }
    }
}
