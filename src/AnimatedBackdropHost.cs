using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Monolith
{
    /// <summary>
    /// Renders custom animated/static backgrounds directly inside Monolith's rounded glass capsule
    /// with ZERO aspect-ratio distortion using GPU-accelerated Brush Viewbox slicing:
    /// - "Crop" (default): Plays an exact user-chosen region/slice (OffsetX, OffsetY, Zoom) of any GIF, MP4/MOV video, or image!
    /// - "Tile": Seamlessly repeats GIFs, videos, or images side-by-side at their natural proportions!
    /// - "Contain": Displays the entire frame centered at its natural aspect ratio.
    /// - 3 built-in smooth GPU-animated live ambient presets (Aurora, Cyber Neon, Lava Sunset).
    /// Never inflates parent SizeToContent width/height and automatically pauses in fullscreen games.
    /// </summary>
    public class AnimatedBackdropHost : Border
    {
        private readonly Grid _layer = new Grid();
        private System.Windows.Controls.Image _imageEl;
        private MediaElement _videoEl;
        private System.Windows.Shapes.Rectangle _brushRect;
        private ImageBrush _imgBrush;
        private VisualBrush _vidBrush;
        private Border _shaderEl;
        private TranslateTransform _shaderTransform;
        private DoubleAnimation _shaderAnim;

        private readonly List<BitmapSource> _gifFrames = new List<BitmapSource>();
        private readonly List<int> _gifDelaysMs = new List<int>();
        private int _gifIndex = 0;
        private DispatcherTimer _gifTimer;

        private string _activeSignature = "";
        private string _fitMode = "Crop";
        private double _offsetX = 0.5;
        private double _offsetY = 0.5;
        private double _zoom = 1.0;
        private bool _paused = false;
        private double _clipRadius = 33;
        private System.Windows.Size _lastArrangedSize = new System.Windows.Size(0, 0);

        public string LastError { get; private set; }
        public double MediaNaturalWidth { get; private set; }
        public double MediaNaturalHeight { get; private set; }
        public event Action MediaReady;

        public AnimatedBackdropHost()
        {
            IsHitTestVisible = false;
            ClipToBounds = true;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            _layer.HorizontalAlignment = HorizontalAlignment.Stretch;
            _layer.VerticalAlignment = VerticalAlignment.Stretch;
            Child = _layer;
        }

        /// <summary>
        /// Return (0,0) desired size so uploaded high-res images/videos NEVER distort or stretch
        /// DockWindow's SizeToContent width/height; ArrangeOverride then fills the exact final capsule size.
        /// </summary>
        protected override System.Windows.Size MeasureOverride(System.Windows.Size constraint)
        {
            base.MeasureOverride(constraint);
            return new System.Windows.Size(0, 0);
        }

        protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
        {
            _lastArrangedSize = finalSize;
            UpdateBrushViewport(finalSize.Width, finalSize.Height);
            System.Windows.Size arranged = base.ArrangeOverride(finalSize);
            ApplyClip(arranged.Width, arranged.Height);
            return arranged;
        }

        public void SetClipRadius(double radius)
        {
            _clipRadius = radius;
            ApplyClip(ActualWidth, ActualHeight);
        }

        private void ApplyClip(double w, double h)
        {
            if (w > 1 && h > 1)
            {
                double r = Math.Min(_clipRadius, Math.Min(w, h) / 2.0);
                RectangleGeometry rg = new RectangleGeometry(new Rect(0, 0, w, h), r, r);
                rg.Freeze();
                Clip = rg;
            }
        }

        public void Configure(string mode, string filePath, double opacity)
        {
            Configure(mode, filePath, opacity, "Crop", 0.5, 0.5, 1.0);
        }

        public void Configure(string mode, string filePath, double opacity, string fitMode)
        {
            Configure(mode, filePath, opacity, fitMode, 0.5, 0.5, 1.0);
        }

        public void Configure(string mode, string filePath, double opacity, string fitMode, double offsetX, double offsetY, double zoom)
        {
            if (string.IsNullOrEmpty(mode)) mode = "None";
            if (string.IsNullOrEmpty(fitMode) || fitMode == "Cover" || fitMode == "Stretch") fitMode = "Crop";
            if (opacity < 0.08) opacity = 0.08;
            if (opacity > 1.0) opacity = 1.0;

            _offsetX = Clamp(offsetX, 0.0, 1.0);
            _offsetY = Clamp(offsetY, 0.0, 1.0);
            _zoom = Clamp(zoom, 1.0, 3.5);
            _fitMode = fitMode;
            Opacity = opacity;

            string sig = mode + "|" + (filePath ?? "") + "|" + fitMode;
            if (sig == _activeSignature)
            {
                UpdateBrushViewport(_lastArrangedSize.Width > 1 ? _lastArrangedSize.Width : ActualWidth,
                                    _lastArrangedSize.Height > 1 ? _lastArrangedSize.Height : ActualHeight);
                return;
            }

            ClearCurrent();
            LastError = null;
            _activeSignature = sig;

            if (mode == "None")
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            Visibility = Visibility.Visible;

            if (mode == "PresetAurora")
            {
                BuildLiveGradientPreset(
                    System.Windows.Media.Color.FromRgb(14, 165, 233),
                    System.Windows.Media.Color.FromRgb(139, 92, 246),
                    System.Windows.Media.Color.FromRgb(236, 72, 153),
                    System.Windows.Media.Color.FromRgb(20, 184, 166),
                    7.5);
                InvalidateArrange();
                return;
            }
            if (mode == "PresetCyber")
            {
                BuildLiveGradientPreset(
                    System.Windows.Media.Color.FromRgb(37, 99, 235),
                    System.Windows.Media.Color.FromRgb(217, 70, 239),
                    System.Windows.Media.Color.FromRgb(6, 182, 212),
                    System.Windows.Media.Color.FromRgb(99, 102, 241),
                    5.5);
                InvalidateArrange();
                return;
            }
            if (mode == "PresetSunset")
            {
                BuildLiveGradientPreset(
                    System.Windows.Media.Color.FromRgb(249, 115, 22),
                    System.Windows.Media.Color.FromRgb(225, 29, 72),
                    System.Windows.Media.Color.FromRgb(124, 58, 237),
                    System.Windows.Media.Color.FromRgb(245, 158, 11),
                    8.5);
                InvalidateArrange();
                return;
            }

            if (mode == "CustomFile")
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    LastError = "Dosya bulunamadı.";
                    Visibility = Visibility.Collapsed;
                    return;
                }

                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".gif")
                {
                    LoadAnimatedGif(filePath);
                }
                else if (ext == ".mp4" || ext == ".wmv" || ext == ".mov" || ext == ".avi" || ext == ".m4v")
                {
                    LoadLoopingVideo(filePath);
                }
                else
                {
                    LoadStaticImage(filePath);
                }
                InvalidateArrange();
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Updates GPU Viewbox slice at 60 FPS with ZERO reload or flicker while dragging the region picker.
        /// </summary>
        public void UpdateRegion(double offsetX, double offsetY, double zoom)
        {
            _offsetX = Clamp(offsetX, 0.0, 1.0);
            _offsetY = Clamp(offsetY, 0.0, 1.0);
            _zoom = Clamp(zoom, 1.0, 3.5);
            double w = _lastArrangedSize.Width > 1 ? _lastArrangedSize.Width : ActualWidth;
            double h = _lastArrangedSize.Height > 1 ? _lastArrangedSize.Height : ActualHeight;
            UpdateBrushViewport(w, h);
        }

        private void UpdateBrushViewport(double dstW, double dstH)
        {
            if (dstW <= 1 || dstH <= 1) return;
            double natW = MediaNaturalWidth > 1 ? MediaNaturalWidth : 16.0;
            double natH = MediaNaturalHeight > 1 ? MediaNaturalHeight : 9.0;
            double natAspect = natW / natH;
            double dstAspect = dstW / dstH;

            TileBrush brush = (_imgBrush != null) ? (TileBrush)_imgBrush : (TileBrush)_vidBrush;
            if (brush == null) return;

            if (_fitMode == "Crop")
            {
                // Exact aspect-preserving normalized Viewbox (100% pixel-for-pixel match with MediaCropPicker!)
                double visWFrac, visHFrac;
                if (dstAspect >= natAspect)
                {
                    visWFrac = 1.0 / _zoom;
                    visHFrac = natAspect / (dstAspect * _zoom);
                }
                else
                {
                    visHFrac = 1.0 / _zoom;
                    visWFrac = dstAspect / (natAspect * _zoom);
                }
                visWFrac = Clamp(visWFrac, 0.02, 1.0);
                visHFrac = Clamp(visHFrac, 0.02, 1.0);

                double vx = (1.0 - visWFrac) * _offsetX;
                double vy = (1.0 - visHFrac) * _offsetY;

                brush.TileMode = TileMode.None;
                brush.Stretch = Stretch.Fill; // Viewbox matches dstAspect so aspect ratio is 1:1 exact!
                brush.ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
                brush.Viewport = new Rect(0, 0, 1, 1);
                brush.ViewboxUnits = BrushMappingMode.RelativeToBoundingBox;
                brush.Viewbox = new Rect(vx, vy, visWFrac, visHFrac);
            }
            else if (_fitMode == "Tile")
            {
                double tileW = Math.Max(16.0, dstH * natAspect);
                brush.TileMode = TileMode.Tile;
                brush.Stretch = Stretch.UniformToFill;
                brush.ViewboxUnits = BrushMappingMode.RelativeToBoundingBox;
                brush.Viewbox = new Rect(0, 0, 1, 1);
                brush.ViewportUnits = BrushMappingMode.Absolute;
                brush.Viewport = new Rect(0, 0, tileW, dstH);
            }
        }

        public void SetPaused(bool paused)
        {
            if (_paused == paused) return;
            _paused = paused;
            try
            {
                if (_gifTimer != null)
                {
                    if (paused) _gifTimer.Stop();
                    else if (_gifFrames.Count > 1) _gifTimer.Start();
                }
                if (_videoEl != null)
                {
                    if (paused) _videoEl.Pause();
                    else _videoEl.Play();
                }
                if (_shaderTransform != null && _shaderAnim != null)
                {
                    if (paused) _shaderTransform.BeginAnimation(TranslateTransform.XProperty, null);
                    else _shaderTransform.BeginAnimation(TranslateTransform.XProperty, _shaderAnim);
                }
            }
            catch { }
        }

        private void BuildLiveGradientPreset(
            System.Windows.Media.Color c0,
            System.Windows.Media.Color c1,
            System.Windows.Media.Color c2,
            System.Windows.Media.Color c3,
            double cycleSeconds)
        {
            LinearGradientBrush lg = new LinearGradientBrush();
            lg.StartPoint = new System.Windows.Point(0, 0.2);
            lg.EndPoint = new System.Windows.Point(1, 0.8);
            lg.SpreadMethod = GradientSpreadMethod.Reflect;
            lg.GradientStops.Add(new GradientStop(c0, 0.0));
            lg.GradientStops.Add(new GradientStop(c1, 0.35));
            lg.GradientStops.Add(new GradientStop(c2, 0.70));
            lg.GradientStops.Add(new GradientStop(c3, 1.0));

            _shaderTransform = new TranslateTransform(0, 0);
            lg.RelativeTransform = _shaderTransform;

            _shaderAnim = new DoubleAnimation(0.0, 2.0, TimeSpan.FromSeconds(cycleSeconds));
            _shaderAnim.RepeatBehavior = RepeatBehavior.Forever;
            if (!_paused)
                _shaderTransform.BeginAnimation(TranslateTransform.XProperty, _shaderAnim);

            _shaderEl = new Border
            {
                Background = lg,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            _layer.Children.Add(_shaderEl);
        }

        private void MountBitmapVisual(BitmapSource initialFrame)
        {
            MediaNaturalWidth = initialFrame.PixelWidth > 0 ? initialFrame.PixelWidth : initialFrame.Width;
            MediaNaturalHeight = initialFrame.PixelHeight > 0 ? initialFrame.PixelHeight : initialFrame.Height;

            if (_fitMode == "Contain")
            {
                _imageEl = new System.Windows.Controls.Image
                {
                    Source = initialFrame,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                RenderOptions.SetBitmapScalingMode(_imageEl, BitmapScalingMode.HighQuality);
                _layer.Children.Add(_imageEl);
            }
            else
            {
                // GPU-accelerated ImageBrush supporting both exact Crop Viewbox & seamless Tile!
                _imgBrush = new ImageBrush(initialFrame);
                RenderOptions.SetBitmapScalingMode(_imgBrush, BitmapScalingMode.HighQuality);
                _brushRect = new System.Windows.Shapes.Rectangle
                {
                    Fill = _imgBrush,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                _layer.Children.Add(_brushRect);
            }

            UpdateBrushViewport(_lastArrangedSize.Width > 1 ? _lastArrangedSize.Width : ActualWidth,
                                _lastArrangedSize.Height > 1 ? _lastArrangedSize.Height : ActualHeight);
            if (MediaReady != null) MediaReady();
        }

        private void LoadStaticImage(string path)
        {
            BitmapSource src = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                using (MemoryStream ms = new MemoryStream(bytes))
                {
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bi.StreamSource = ms;
                    bi.EndInit();
                    bi.Freeze();
                    src = bi;
                }
            }
            catch
            {
                try
                {
                    using (System.Drawing.Image gImg = System.Drawing.Image.FromFile(path))
                    {
                        double scale = gImg.Width > 1400 ? (1400.0 / gImg.Width) : 1.0;
                        int w = Math.Max(16, (int)Math.Round(gImg.Width * scale));
                        int h = Math.Max(16, (int)Math.Round(gImg.Height * scale));
                        using (Bitmap bmp = new Bitmap(w, h))
                        {
                            using (Graphics g = Graphics.FromImage(bmp))
                            {
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                g.DrawImage(gImg, 0, 0, w, h);
                            }
                            IntPtr hBmp = bmp.GetHbitmap();
                            try
                            {
                                BitmapSource bs = Imaging.CreateBitmapSourceFromHBitmap(
                                    hBmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                                bs.Freeze();
                                src = bs;
                            }
                            finally
                            {
                                DeleteObject(hBmp);
                            }
                        }
                    }
                }
                catch (Exception ex2)
                {
                    LastError = "Görsel yüklenemedi (" + Path.GetExtension(path) + "): " + ex2.Message;
                }
            }

            if (src != null)
                MountBitmapVisual(src);
        }

        private void LoadAnimatedGif(string path)
        {
            try
            {
                using (System.Drawing.Image img = System.Drawing.Image.FromFile(path))
                {
                    FrameDimension dim = new FrameDimension(img.FrameDimensionsList[0]);
                    int rawCount = img.GetFrameCount(dim);
                    int stride = rawCount > 72 ? (int)Math.Ceiling(rawCount / 72.0) : 1;

                    byte[] delayBytes = null;
                    try
                    {
                        PropertyItem prop = img.GetPropertyItem(0x5100);
                        if (prop != null) delayBytes = prop.Value;
                    }
                    catch { }

                    // Cap decoded GIF frames to 640x360 (cuts uncompressed GIF RAM by ~85% while staying razor-sharp in dock!)
                    double scale = 1.0;
                    if (img.Width > 640) scale = Math.Min(scale, 640.0 / img.Width);
                    if (img.Height > 360) scale = Math.Min(scale, 360.0 / img.Height);
                    int dstW = Math.Max(16, (int)Math.Round(img.Width * scale));
                    int dstH = Math.Max(16, (int)Math.Round(img.Height * scale));

                    using (Bitmap bmp = new Bitmap(dstW, dstH))
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                        for (int i = 0; i < rawCount && _gifFrames.Count < 72; i += stride)
                        {
                            img.SelectActiveFrame(dim, i);
                            g.Clear(System.Drawing.Color.Transparent);
                            g.DrawImage(img, 0, 0, dstW, dstH);
                            IntPtr hBmp = bmp.GetHbitmap();
                            try
                            {
                                BitmapSource bs = Imaging.CreateBitmapSourceFromHBitmap(
                                    hBmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                                bs.Freeze();
                                _gifFrames.Add(bs);
                            }
                            finally
                            {
                                DeleteObject(hBmp);
                            }

                            int delayMs = 66 * stride;
                            if (delayBytes != null && i * 4 + 3 < delayBytes.Length)
                            {
                                int cs = BitConverter.ToInt32(delayBytes, i * 4);
                                delayMs = Math.Max(35, cs * 10 * stride);
                            }
                            _gifDelaysMs.Add(delayMs);
                        }
                    }
                }

                if (_gifFrames.Count <= 1)
                {
                    try
                    {
                        using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            GifBitmapDecoder dec = new GifBitmapDecoder(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                            if (dec.Frames.Count > 1)
                            {
                                _gifFrames.Clear();
                                _gifDelaysMs.Clear();
                                int maxF = Math.Min(120, dec.Frames.Count);
                                for (int i = 0; i < maxF; i++)
                                {
                                    BitmapSource fr = dec.Frames[i];
                                    if (fr.CanFreeze && !fr.IsFrozen) fr.Freeze();
                                    _gifFrames.Add(fr);

                                    int dMs = 66;
                                    try
                                    {
                                        BitmapMetadata meta = dec.Frames[i].Metadata as BitmapMetadata;
                                        if (meta != null && meta.ContainsQuery("/grctlext/Delay"))
                                        {
                                            object v = meta.GetQuery("/grctlext/Delay");
                                            if (v != null) dMs = Math.Max(30, Convert.ToInt32(v) * 10);
                                        }
                                    }
                                    catch { }
                                    _gifDelaysMs.Add(dMs);
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (_gifFrames.Count == 0)
                {
                    LoadStaticImage(path);
                    return;
                }

                MountBitmapVisual(_gifFrames[0]);

                if (_gifFrames.Count > 1)
                {
                    _gifIndex = 0;
                    _gifTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_gifDelaysMs[0]) };
                    _gifTimer.Tick += delegate
                    {
                        if (_gifFrames.Count == 0) return;
                        _gifIndex = (_gifIndex + 1) % _gifFrames.Count;
                        BitmapSource fr = _gifFrames[_gifIndex];
                        if (_imageEl != null) _imageEl.Source = fr;
                        if (_imgBrush != null) _imgBrush.ImageSource = fr;
                        _gifTimer.Interval = TimeSpan.FromMilliseconds(_gifDelaysMs[_gifIndex]);
                    };
                    if (!_paused) _gifTimer.Start();
                }
            }
            catch
            {
                LoadStaticImage(path);
            }
        }

        private void LoadLoopingVideo(string path)
        {
            try
            {
                MediaNaturalWidth = 16.0;
                MediaNaturalHeight = 9.0;

                _videoEl = new MediaElement
                {
                    Source = new Uri(path, UriKind.Absolute),
                    LoadedBehavior = System.Windows.Controls.MediaState.Manual,
                    UnloadedBehavior = System.Windows.Controls.MediaState.Manual,
                    IsMuted = true,
                    Volume = 0,
                    Stretch = (_fitMode == "Contain") ? Stretch.Uniform : Stretch.Fill,
                    ScrubbingEnabled = true
                };
                MediaElement captured = _videoEl;
                captured.Loaded += delegate
                {
                    try { if (!_paused) captured.Play(); } catch { }
                };
                captured.MediaOpened += delegate
                {
                    try
                    {
                        if (captured.NaturalVideoWidth > 0 && captured.NaturalVideoHeight > 0)
                        {
                            MediaNaturalWidth = captured.NaturalVideoWidth;
                            MediaNaturalHeight = captured.NaturalVideoHeight;
                            if (_fitMode != "Contain")
                            {
                                captured.Width = captured.NaturalVideoWidth;
                                captured.Height = captured.NaturalVideoHeight;
                            }
                            UpdateBrushViewport(_lastArrangedSize.Width > 1 ? _lastArrangedSize.Width : ActualWidth,
                                                _lastArrangedSize.Height > 1 ? _lastArrangedSize.Height : ActualHeight);
                            if (MediaReady != null) MediaReady();
                        }
                        if (!_paused) captured.Play();
                    }
                    catch { }
                };
                captured.MediaEnded += delegate
                {
                    try
                    {
                        captured.Position = TimeSpan.Zero;
                        if (!_paused) captured.Play();
                    }
                    catch { }
                };
                captured.MediaFailed += delegate(object s, ExceptionRoutedEventArgs e)
                {
                    LastError = "Video açılamadı (MP4/WMV önerilir).";
                };

                if (_fitMode == "Contain")
                {
                    captured.HorizontalAlignment = HorizontalAlignment.Center;
                    captured.VerticalAlignment = VerticalAlignment.Center;
                    _layer.Children.Add(captured);
                }
                else
                {
                    // Keep active MediaElement decoding smoothly while VisualBrush renders exact Viewbox Crop/Tile!
                    captured.Width = 640;
                    captured.Height = 360;
                    captured.Opacity = 0.01;
                    _layer.Children.Add(captured);

                    _vidBrush = new VisualBrush(captured);
                    _brushRect = new System.Windows.Shapes.Rectangle
                    {
                        Fill = _vidBrush,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
                    };
                    _layer.Children.Add(_brushRect);
                    UpdateBrushViewport(_lastArrangedSize.Width > 1 ? _lastArrangedSize.Width : ActualWidth,
                                        _lastArrangedSize.Height > 1 ? _lastArrangedSize.Height : ActualHeight);
                }

                if (IsLoaded && !_paused) _videoEl.Play();
            }
            catch (Exception ex)
            {
                LastError = "Video yüklenemedi: " + ex.Message;
            }
        }

        public void ClearCurrent()
        {
            if (_gifTimer != null)
            {
                _gifTimer.Stop();
                _gifTimer = null;
            }
            _gifFrames.Clear();
            _gifDelaysMs.Clear();
            if (_videoEl != null)
            {
                try { _videoEl.Stop(); _videoEl.Close(); } catch { }
                _videoEl = null;
            }
            if (_shaderTransform != null)
            {
                try { _shaderTransform.BeginAnimation(TranslateTransform.XProperty, null); } catch { }
                _shaderTransform = null;
            }
            _shaderAnim = null;
            _imageEl = null;
            _brushRect = null;
            _imgBrush = null;
            _vidBrush = null;
            _shaderEl = null;
            _layer.Children.Clear();
            _activeSignature = "";
        }

        private static double Clamp(double v, double min, double max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
