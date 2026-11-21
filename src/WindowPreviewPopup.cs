using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Monolith
{
    /// <summary>
    /// Live GPU-composited window thumbnail preview popup for Monolith
    /// powered by Windows DWM (DwmRegisterThumbnail). Renders real-time 60 FPS video/content
    /// of any hovered open window with Windows 11 native rounded corners, click-to-focus,
    /// and a quick close ('x') button.
    /// </summary>
    public class WindowPreviewPopup : Window
    {
        private IntPtr _thumbHandle = IntPtr.Zero;
        private IntPtr _targetWindow = IntPtr.Zero;
        private readonly DispatcherTimer _hideDelay;

        private readonly Border _rootBorder;
        private readonly TextBlock _titleText;
        private readonly Image _appIcon;
        private readonly Border _previewHost;
        private readonly Border _closeBtn;
        private readonly TextBlock _closeTxt;

        private bool _isPointerInside = false;
        private Action<IntPtr> _onFocusWindow;
        private Action<IntPtr> _onCloseWindow;

        public WindowPreviewPopup(Action<IntPtr> onFocusWindow, Action<IntPtr> onCloseWindow)
        {
            _onFocusWindow = onFocusWindow;
            _onCloseWindow = onCloseWindow;

            // IMPORTANT: AllowsTransparency MUST be false for DwmRegisterThumbnail to render!
            WindowStyle = WindowStyle.None;
            AllowsTransparency = false;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            Width = 282;
            Height = 200;

            _rootBorder = new Border
            {
                BorderThickness = new Thickness(1.2),
                Padding = new Thickness(11, 8, 10, 11)
            };

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Header row: icon + title + large easy-to-click close button
            Grid hdr = new Grid();
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _appIcon = new Image
            {
                Width = 18,
                Height = 18,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(_appIcon, BitmapScalingMode.HighQuality);
            Grid.SetColumn(_appIcon, 0);
            hdr.Children.Add(_appIcon);

            _titleText = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.2,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(_titleText, 1);
            hdr.Children.Add(_titleText);

            _closeTxt = new TextBlock
            {
                Text = "\u2715",
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0)
            };
            _closeBtn = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = ThemeManager.Brush(Color.FromArgb(46, 239, 68, 68)),
                BorderBrush = ThemeManager.Brush(Color.FromArgb(95, 248, 113, 113)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Pencereyi Kapat",
                Child = _closeTxt
            };
            _closeBtn.MouseEnter += delegate
            {
                _closeBtn.Background = ThemeManager.Brush(Color.FromRgb(239, 68, 68));
                _closeBtn.BorderBrush = ThemeManager.Brush(Color.FromRgb(252, 165, 165));
                _closeTxt.Foreground = Brushes.White;
            };
            _closeBtn.MouseLeave += delegate
            {
                _closeBtn.Background = ThemeManager.Brush(Color.FromArgb(46, 239, 68, 68));
                _closeBtn.BorderBrush = ThemeManager.Brush(Color.FromArgb(95, 248, 113, 113));
                _closeTxt.Foreground = ThemeManager.Brush(Color.FromRgb(252, 165, 165));
            };
            _closeBtn.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                IntPtr h = _targetWindow;
                HideImmediate();
                if (h != IntPtr.Zero && _onCloseWindow != null)
                    _onCloseWindow(h);
            };
            Grid.SetColumn(_closeBtn, 2);
            hdr.Children.Add(_closeBtn);

            Grid.SetRow(hdr, 0);
            layout.Children.Add(hdr);

            // Live DWM thumbnail host placeholder
            _previewHost = new Border
            {
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 6, 0, 0),
                Cursor = Cursors.Hand
            };
            Grid.SetRow(_previewHost, 1);
            layout.Children.Add(_previewHost);

            _rootBorder.Child = layout;
            Content = _rootBorder;

            MouseEnter += delegate
            {
                _isPointerInside = true;
                _hideDelay.Stop();
            };
            MouseLeave += delegate
            {
                _isPointerInside = false;
                ScheduleHide();
            };
            MouseLeftButtonUp += delegate
            {
                IntPtr h = _targetWindow;
                HideImmediate();
                if (h != IntPtr.Zero && _onFocusWindow != null)
                    _onFocusWindow(h);
            };

            _hideDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            _hideDelay.Tick += delegate
            {
                _hideDelay.Stop();
                if (!_isPointerInside)
                    HideImmediate();
            };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int ex = NativeWin32.GetWindowLong(hwnd, NativeWin32.GWL_EXSTYLE);
            NativeWin32.SetWindowLong(hwnd, NativeWin32.GWL_EXSTYLE, ex | NativeWin32.WS_EX_TOOLWINDOW | NativeWin32.WS_EX_NOACTIVATE);

            // Apply Windows 11 native rounded corners (DWMWCP_ROUND = 2)
            try
            {
                int pref = 2;
                NativeWin32.DwmSetWindowAttribute(hwnd, NativeWin32.DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
            }
            catch { }
        }

        public void ShowPreview(IntPtr targetWindow, string title, ImageSource icon, FrameworkElement anchor, Window ownerDock, bool isTop, ThemePalette palette)
        {
            if (targetWindow == IntPtr.Zero || !NativeWin32.IsWindow(targetWindow)) return;
            _hideDelay.Stop();
            _targetWindow = targetWindow;

            // Theme styling
            Color bg = palette.IsLight ? Color.FromRgb(245, 247, 251) : Color.FromRgb(20, 23, 31);
            Color hostBg = palette.IsLight ? Color.FromRgb(226, 232, 240) : Color.FromRgb(12, 14, 20);
            Background = ThemeManager.Brush(bg);
            _rootBorder.Background = ThemeManager.Brush(bg);
            _rootBorder.BorderBrush = ThemeManager.Brush(palette.ModalBorder);
            _titleText.Foreground = ThemeManager.Brush(palette.TextPrimary);
            _titleText.Text = title ?? "";
            _closeTxt.Foreground = ThemeManager.Brush(palette.IsLight ? Color.FromRgb(220, 38, 38) : Color.FromRgb(252, 165, 165));
            _closeBtn.Background = ThemeManager.Brush(Color.FromArgb(46, 239, 68, 68));
            _closeBtn.BorderBrush = ThemeManager.Brush(Color.FromArgb(95, 248, 113, 113));
            _previewHost.Background = ThemeManager.Brush(hostBg);
            _appIcon.Source = icon;
            _appIcon.Visibility = (icon != null) ? Visibility.Visible : Visibility.Collapsed;

            // Position directly above/below hovered dock tile
            try
            {
                Point pt = anchor.PointToScreen(new Point(anchor.ActualWidth / 2.0, 0));
                PresentationSource src = PresentationSource.FromVisual(ownerDock);
                double dpiX = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformToDevice.M11 : 1.0;

                double cx = pt.X / dpiX;
                double left = cx - Width / 2.0;
                double sw = SystemParameters.PrimaryScreenWidth;
                if (left < 8) left = 8;
                if (left + Width > sw - 8) left = sw - Width - 8;

                Left = left;
                Top = isTop ? (ownerDock.Top + ownerDock.ActualHeight + 2) : (ownerDock.Top - Height - 2);
            }
            catch { }

            if (!IsVisible) Show();
            UpdateLayout();
            BindDwmThumbnail(targetWindow);
        }

        private void BindDwmThumbnail(IntPtr targetWindow)
        {
            ReleaseThumbnail();
            try
            {
                IntPtr destHwnd = new WindowInteropHelper(this).Handle;
                if (destHwnd == IntPtr.Zero) return;

                if (NativeWin32.DwmRegisterThumbnail(destHwnd, targetWindow, out _thumbHandle) != 0 || _thumbHandle == IntPtr.Zero)
                    return;

                NativeWin32.PSIZE srcSize;
                if (NativeWin32.DwmQueryThumbnailSourceSize(_thumbHandle, out srcSize) != 0 || srcSize.x <= 0 || srcSize.y <= 0)
                {
                    srcSize.x = 16;
                    srcSize.y = 9;
                }

                PresentationSource ps = PresentationSource.FromVisual(this);
                double dpiX = ps != null && ps.CompositionTarget != null ? ps.CompositionTarget.TransformToDevice.M11 : 1.0;
                double dpiY = ps != null && ps.CompositionTarget != null ? ps.CompositionTarget.TransformToDevice.M22 : 1.0;

                Point hostOrigin = _previewHost.TranslatePoint(new Point(0, 0), this);
                int boxL = (int)Math.Round(hostOrigin.X * dpiX) + 2;
                int boxT = (int)Math.Round(hostOrigin.Y * dpiY) + 2;
                int boxW = Math.Max(40, (int)Math.Round(_previewHost.ActualWidth * dpiX) - 4);
                int boxH = Math.Max(40, (int)Math.Round(_previewHost.ActualHeight * dpiY) - 4);

                // Aspect-fit live thumbnail inside preview box
                double scale = Math.Min((double)boxW / srcSize.x, (double)boxH / srcSize.y);
                int drawW = Math.Max(20, (int)Math.Round(srcSize.x * scale));
                int drawH = Math.Max(20, (int)Math.Round(srcSize.y * scale));
                int drawL = boxL + (boxW - drawW) / 2;
                int drawT = boxT + (boxH - drawH) / 2;

                NativeWin32.DWM_THUMBNAIL_PROPERTIES props = new NativeWin32.DWM_THUMBNAIL_PROPERTIES();
                props.dwFlags = NativeWin32.DWM_TNP_RECTDESTINATION | NativeWin32.DWM_TNP_VISIBLE | NativeWin32.DWM_TNP_OPACITY | NativeWin32.DWM_TNP_SOURCECLIENTAREAONLY;
                props.rcDestination = new NativeWin32.RECT { Left = drawL, Top = drawT, Right = drawL + drawW, Bottom = drawT + drawH };
                props.opacity = 255;
                props.fVisible = true;
                props.fSourceClientAreaOnly = false;

                NativeWin32.DwmUpdateThumbnailProperties(_thumbHandle, ref props);
            }
            catch { }
        }

        private void ReleaseThumbnail()
        {
            if (_thumbHandle != IntPtr.Zero)
            {
                try { NativeWin32.DwmUnregisterThumbnail(_thumbHandle); } catch { }
                _thumbHandle = IntPtr.Zero;
            }
        }

        public void ScheduleHide()
        {
            _hideDelay.Stop();
            _hideDelay.Start();
        }

        public void HideImmediate()
        {
            _hideDelay.Stop();
            _isPointerInside = false;
            ReleaseThumbnail();
            _targetWindow = IntPtr.Zero;
            if (IsVisible) Hide();
        }

        protected override void OnClosed(EventArgs e)
        {
            ReleaseThumbnail();
            base.OnClosed(e);
        }
    }
}
