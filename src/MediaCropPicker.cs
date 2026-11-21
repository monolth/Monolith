using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Monolith
{
    /// <summary>
    /// Interactive live region/slice picker for Monolith's animated dock backgrounds (GIF / MP4 / Image).
    /// Plays the full original media behind a darkened mask with a draggable, zoomable taskbar pill
    /// selection window so the user can pick any exact region of an animated GIF, video, or photo!
    /// </summary>
    public class MediaCropPicker : Border
    {
        private const double CanvasW = 456.0;
        private const double CanvasH = 186.0;

        private readonly ThemePalette _palette;
        private readonly AnimatedBackdropHost _fullMedia;
        private readonly Canvas _overlay;
        private readonly System.Windows.Shapes.Path _dimMaskPath;
        private readonly Border _selFrame;
        private readonly Border _selInnerRim;

        private double _dockAspect = 9.5;
        private double _offsetX = 0.5;
        private double _offsetY = 0.5;
        private double _zoom = 1.0;

        private bool _dragging = false;
        private Point _dragStartPt;
        private double _dragStartOffX;
        private double _dragStartOffY;

        // Cached rendered media rectangle inside CanvasW x CanvasH
        private double _imgX, _imgY, _imgW, _imgH;
        private double _selX, _selY, _selW, _selH;

        public event Action<double, double, double, bool> RegionChanged;

        public string LastError
        {
            get { return _fullMedia.LastError; }
        }

        public MediaCropPicker(MonolithConfig cfg, ThemePalette palette, double dockAspect)
        {
            _palette = palette;
            if (dockAspect >= 4.0 && dockAspect <= 20.0) _dockAspect = dockAspect;
            _offsetX = Clamp(cfg.DockMediaOffsetX, 0.0, 1.0);
            _offsetY = Clamp(cfg.DockMediaOffsetY, 0.0, 1.0);
            _zoom = Clamp(cfg.DockMediaZoom, 1.0, 3.5);

            Width = CanvasW;
            Height = CanvasH;
            HorizontalAlignment = HorizontalAlignment.Left;
            CornerRadius = new CornerRadius(12);
            Background = ThemeManager.Brush(Color.FromRgb(10, 13, 22));
            BorderBrush = ThemeManager.Brush(palette.ModalBorder);
            BorderThickness = new Thickness(1.2);
            ClipToBounds = true;
            Cursor = Cursors.SizeAll;

            Grid root = new Grid { Width = CanvasW, Height = CanvasH, ClipToBounds = true };

            // Layer 0: Full animated GIF / Video / Image playing live in natural aspect ratio
            _fullMedia = new AnimatedBackdropHost();
            _fullMedia.SetClipRadius(11);
            root.Children.Add(_fullMedia);

            // Layer 1: 4-quadrant dimmed mask + luminous taskbar selection frame
            _overlay = new Canvas
            {
                Width = CanvasW,
                Height = CanvasH,
                Background = Brushes.Transparent, // Hit-testable across full canvas
                ClipToBounds = true
            };

            Brush dimBrush = ThemeManager.Brush(Color.FromArgb(152, 6, 9, 16));
            _dimMaskPath = new System.Windows.Shapes.Path
            {
                Fill = dimBrush,
                IsHitTestVisible = false
            };
            _overlay.Children.Add(_dimMaskPath);

            _selInnerRim = new Border
            {
                BorderBrush = ThemeManager.Brush(Color.FromArgb(200, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                IsHitTestVisible = false
            };
            _selFrame = new Border
            {
                BorderBrush = ThemeManager.Brush(palette.Accent),
                BorderThickness = new Thickness(2.2),
                Background = Brushes.Transparent,
                IsHitTestVisible = false,
                Child = _selInnerRim
            };
            _overlay.Children.Add(_selFrame);

            // Subtle instructional hint pill at top-right
            Border hint = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = ThemeManager.Brush(Color.FromArgb(180, 10, 14, 24)),
                BorderBrush = ThemeManager.Brush(Color.FromArgb(75, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 3.5, 8, 3.5),
                IsHitTestVisible = false
            };
            hint.Child = new TextBlock
            {
                Text = L10n.T(cfg, "Sürükle: Bölge Seç  •  Tekerlek: Yakınlaştır", "Drag: Pick Region  •  Wheel: Zoom"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            Canvas.SetRight(hint, 8);
            Canvas.SetTop(hint, 8);
            _overlay.Children.Add(hint);

            _overlay.MouseLeftButtonDown += OnOverlayMouseDown;
            _overlay.MouseMove += OnOverlayMouseMove;
            _overlay.MouseLeftButtonUp += OnOverlayMouseUp;
            _overlay.PreviewMouseWheel += OnOverlayMouseWheel;

            root.Children.Add(_overlay);
            Child = root;

            _fullMedia.MediaReady += delegate { UpdateOverlayRects(); };
            _fullMedia.Configure("CustomFile", cfg.DockMediaPath, 1.0, "Contain");

            Loaded += delegate { UpdateOverlayRects(); };
            UpdateOverlayRects();
        }

        public void SetRegionExternal(double offsetX, double offsetY, double zoom)
        {
            _offsetX = Clamp(offsetX, 0.0, 1.0);
            _offsetY = Clamp(offsetY, 0.0, 1.0);
            _zoom = Clamp(zoom, 1.0, 3.5);
            UpdateOverlayRects();
        }

        public void DisposeMedia()
        {
            try { _fullMedia.ClearCurrent(); } catch { }
        }

        private void UpdateOverlayRects()
        {
            if (_dimMaskPath == null || _selFrame == null) return;
            double natW = _fullMedia.MediaNaturalWidth > 1 ? _fullMedia.MediaNaturalWidth : 16.0;
            double natH = _fullMedia.MediaNaturalHeight > 1 ? _fullMedia.MediaNaturalHeight : 9.0;
            double natAspect = natW / natH;

            // Compute exact rendered bounds of Contain media inside CanvasW x CanvasH
            double fitScale = Math.Min(CanvasW / natW, CanvasH / natH);
            _imgW = natW * fitScale;
            _imgH = natH * fitScale;
            _imgX = (CanvasW - _imgW) / 2.0;
            _imgY = (CanvasH - _imgH) / 2.0;

            // Exact visible slice fraction matching AnimatedBackdropHost's Viewbox Crop math
            double visWFrac, visHFrac;
            if (_dockAspect >= natAspect)
            {
                visWFrac = 1.0 / _zoom;
                visHFrac = natAspect / (_dockAspect * _zoom);
            }
            else
            {
                visHFrac = 1.0 / _zoom;
                visWFrac = _dockAspect / (natAspect * _zoom);
            }
            visWFrac = Clamp(visWFrac, 0.06, 1.0);
            visHFrac = Clamp(visHFrac, 0.08, 1.0);

            _selW = Math.Max(28.0, _imgW * visWFrac);
            _selH = Math.Max(16.0, _imgH * visHFrac);
            if (_selW > _imgW) _selW = _imgW;
            if (_selH > _imgH) _selH = _imgH;

            double panW = Math.Max(0.0, _imgW - _selW);
            double panH = Math.Max(0.0, _imgH - _selH);
            _selX = _imgX + panW * _offsetX;
            _selY = _imgY + panH * _offsetY;

            // Cut a crisp rounded pill-shaped window out of the dimmed mask (zero corner triangles!)
            double r = Math.Min(_selH / 2.0, 14.0);
            CombinedGeometry holeMask = new CombinedGeometry(
                GeometryCombineMode.Exclude,
                new RectangleGeometry(new Rect(0, 0, CanvasW, CanvasH)),
                new RectangleGeometry(new Rect(_selX, _selY, _selW, _selH), r, r));
            holeMask.Freeze();
            _dimMaskPath.Data = holeMask;

            // Position luminous rounded taskbar selection window
            Canvas.SetLeft(_selFrame, _selX);
            Canvas.SetTop(_selFrame, _selY);
            _selFrame.Width = _selW;
            _selFrame.Height = _selH;
            _selFrame.CornerRadius = new CornerRadius(r);
            _selInnerRim.CornerRadius = new CornerRadius(Math.Max(2.0, r - 2.0));
        }

        private void OnOverlayMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Point pt = e.GetPosition(_overlay);
            _dragging = true;
            _overlay.CaptureMouse();

            bool insideSel = (pt.X >= _selX && pt.X <= _selX + _selW && pt.Y >= _selY && pt.Y <= _selY + _selH);
            if (!insideSel)
            {
                // Jump slice center straight to clicked point for instant positioning!
                double panW = Math.Max(0.0, _imgW - _selW);
                double panH = Math.Max(0.0, _imgH - _selH);
                if (panW > 0.5)
                    _offsetX = Clamp((pt.X - _imgX - _selW / 2.0) / panW, 0.0, 1.0);
                if (panH > 0.5)
                    _offsetY = Clamp((pt.Y - _imgY - _selH / 2.0) / panH, 0.0, 1.0);
                UpdateOverlayRects();
                RaiseChanged(false);
            }

            _dragStartPt = pt;
            _dragStartOffX = _offsetX;
            _dragStartOffY = _offsetY;
        }

        private void OnOverlayMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            Point pt = e.GetPosition(_overlay);
            double dx = pt.X - _dragStartPt.X;
            double dy = pt.Y - _dragStartPt.Y;

            double panW = Math.Max(0.0, _imgW - _selW);
            double panH = Math.Max(0.0, _imgH - _selH);

            if (panW > 0.5)
                _offsetX = Clamp(_dragStartOffX + dx / panW, 0.0, 1.0);
            if (panH > 0.5)
                _offsetY = Clamp(_dragStartOffY + dy / panH, 0.0, 1.0);

            UpdateOverlayRects();
            RaiseChanged(false);
        }

        private void OnOverlayMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            _overlay.ReleaseMouseCapture();
            RaiseChanged(true);
        }

        private void OnOverlayMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            double step = (e.Delta > 0) ? 0.15 : -0.15;
            _zoom = Math.Round(Clamp(_zoom + step, 1.0, 3.5), 2);
            UpdateOverlayRects();
            RaiseChanged(true);
        }

        private void RaiseChanged(bool commit)
        {
            if (RegionChanged != null)
                RegionChanged(_offsetX, _offsetY, _zoom, commit);
        }

        private static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
