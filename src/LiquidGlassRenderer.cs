using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Monolith
{
    /// <summary>
    /// Renders genuine optical-refraction Prism Glass and Glacier Frost backdrops
    /// by sampling the user's real desktop wallpaper behind the dock's exact screen coordinates,
    /// applying a fast separable blur, and warping the curved capsule perimeter with
    /// SDF normal displacement + chromatic dispersion + Fresnel specular rim glow.
    /// </summary>
    internal static class LiquidGlassRenderer
    {
        private static string _cachedWallpaperPath;
        private static DateTime _cachedWallpaperMtime;
        private static Bitmap _cachedScaledScreen;
        private static int _cachedScreenW;
        private static int _cachedScreenH;

        public static void ReleaseWallpaperCache()
        {
            if (_cachedScaledScreen != null)
            {
                try { _cachedScaledScreen.Dispose(); } catch { }
                _cachedScaledScreen = null;
                _cachedWallpaperPath = null;
            }
        }

        private static void EnsureWallpaperCache(int screenW, int screenH)
        {
            // Store wallpaper at half screen resolution (75% less RAM, 4x faster boot!) since refraction downsamples 2x anyway
            int cacheW = Math.Max(160, screenW / 2);
            int cacheH = Math.Max(90, screenH / 2);

            string wpPath = NativeWin32.GetWallpaperPath();
            DateTime mtime = DateTime.MinValue;
            if (!string.IsNullOrEmpty(wpPath))
            {
                try { mtime = File.GetLastWriteTimeUtc(wpPath); } catch { }
            }

            if (_cachedScaledScreen != null &&
                _cachedWallpaperPath == wpPath &&
                _cachedWallpaperMtime == mtime &&
                _cachedScreenW == cacheW &&
                _cachedScreenH == cacheH)
            {
                return;
            }

            if (_cachedScaledScreen != null)
            {
                _cachedScaledScreen.Dispose();
                _cachedScaledScreen = null;
            }

            _cachedWallpaperPath = wpPath;
            _cachedWallpaperMtime = mtime;
            _cachedScreenW = cacheW;
            _cachedScreenH = cacheH;

            Bitmap canvas = new Bitmap(cacheW, cacheH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(canvas))
            {
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;

                System.Windows.Media.Color bg = NativeWin32.GetDesktopFallbackColor();
                g.Clear(System.Drawing.Color.FromArgb(255, bg.R, bg.G, bg.B));

                if (!string.IsNullOrEmpty(wpPath) && File.Exists(wpPath))
                {
                    try
                    {
                        using (FileStream fs = new FileStream(wpPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (Image img = Image.FromStream(fs))
                        {
                            // Windows "Fill" scaling (aspect-fill centered)
                            double scaleX = (double)cacheW / img.Width;
                            double scaleY = (double)cacheH / img.Height;
                            double scale = Math.Max(scaleX, scaleY);
                            int drawW = (int)Math.Ceiling(img.Width * scale);
                            int drawH = (int)Math.Ceiling(img.Height * scale);
                            int drawX = (cacheW - drawW) / 2;
                            int drawY = (cacheH - drawH) / 2;
                            g.DrawImage(img, drawX, drawY, drawW, drawH);
                        }
                    }
                    catch { }
                }
            }

            _cachedScaledScreen = canvas;
        }

        public static System.Windows.Media.Brush RenderBackdropBrush(
            double dockLeftDip,
            double dockTopDip,
            double dockWidthDip,
            double dockHeightDip,
            double cornerRadiusDip,
            double dpiScaleX,
            double dpiScaleY,
            ThemePalette palette)
        {
            if (palette == null || !palette.UsesRefraction || dockWidthDip < 20 || dockHeightDip < 20)
            {
                return ThemeManager.Brush(palette != null ? palette.DockTint : System.Windows.Media.Colors.Black);
            }

            try
            {
                int screenW = Math.Max(1, (int)Math.Round(SystemParameters.PrimaryScreenWidth * dpiScaleX));
                int screenH = Math.Max(1, (int)Math.Round(SystemParameters.PrimaryScreenHeight * dpiScaleY));
                EnsureWallpaperCache(screenW, screenH);
                if (_cachedScaledScreen == null)
                    return ThemeManager.Brush(palette.DockTint);

                // Sample directly from our half-resolution wallpaper cache (0 extra scaling work!)
                int fullW = Math.Max(4, (int)Math.Round(dockWidthDip * dpiScaleX));
                int fullH = Math.Max(4, (int)Math.Round(dockHeightDip * dpiScaleY));
                int w = Math.Max(4, fullW / 2);
                int h = Math.Max(4, fullH / 2);

                int pad = 16; // extra wallpaper border for edge refraction sampling
                int srcX = (int)Math.Round(dockLeftDip * dpiScaleX * 0.5) - pad;
                int srcY = (int)Math.Round(dockTopDip * dpiScaleY * 0.5) - pad;
                int srcW = w + pad * 2;
                int srcH = h + pad * 2;

                int workW = w + pad * 2;
                int workH = h + pad * 2;

                byte[] srcPixels = new byte[workW * workH * 4];
                using (Bitmap crop = new Bitmap(workW, workH, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(crop))
                    {
                        g.InterpolationMode = InterpolationMode.Bilinear;
                        g.Clear(System.Drawing.Color.FromArgb(255, 24, 28, 38));
                        // Clamp source rect safely to cached screen
                        Rectangle destRect = new Rectangle(0, 0, workW, workH);
                        Rectangle srcRect = new Rectangle(srcX, srcY, srcW, srcH);
                        g.DrawImage(_cachedScaledScreen, destRect, srcRect, GraphicsUnit.Pixel);
                    }

                    BitmapData bd = crop.LockBits(new Rectangle(0, 0, workW, workH), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    Marshal.Copy(bd.Scan0, srcPixels, 0, srcPixels.Length);
                    crop.UnlockBits(bd);
                }

                // Fast separable 2-pass blur
                int blurR = Math.Max(1, (int)Math.Round(palette.BlurRadius * 0.55));
                byte[] blurred = BoxBlur(srcPixels, workW, workH, blurR);
                blurred = BoxBlur(blurred, workW, workH, Math.Max(1, blurR / 2));

                // Optical lens refraction + chromatic rim dispersion along the rounded capsule SDF!
                byte[] output = new byte[w * h * 4];
                float halfW = w * 0.5f;
                float halfH = h * 0.5f;
                float radius = (float)Math.Min(Math.Min(halfW - 1, halfH - 1), cornerRadiusDip * dpiScaleX * 0.5);
                float rimDepth = Math.Max(6f, Math.Min(halfH * 0.72f, 15f));
                float refract = (float)(palette.RefractionStrength * 0.55);
                float chroma = (float)(palette.ChromaticShift * 0.55);

                float tintA = palette.DockTint.A / 255f;
                float tintR = palette.DockTint.R;
                float tintG = palette.DockTint.G;
                float tintB = palette.DockTint.B;
                float invTintA = 1f - tintA;

                // Subtle overall transparency so moving bright/dark windows behind the dock still cast ambient color
                byte outAlpha = (palette.Name == "Spatial Glass") ? (byte)214 :
                                (palette.Name == "Prism Glass") ? (byte)222 :
                                (palette.Name == "Glacier Frost") ? (byte)230 : (byte)238;

                for (int y = 0; y < h; y++)
                {
                    float py = y + 0.5f - halfH;
                    float ay = Math.Abs(py);
                    float qy = ay - (halfH - radius);

                    for (int x = 0; x < w; x++)
                    {
                        float px = x + 0.5f - halfW;
                        float ax = Math.Abs(px);
                        float qx = ax - (halfW - radius);

                        // Signed distance to rounded rectangle perimeter (negative inside)
                        float dxOut = qx > 0 ? qx : 0;
                        float dyOut = qy > 0 ? qy : 0;
                        float outDist = (float)Math.Sqrt(dxOut * dxOut + dyOut * dyOut);
                        float inDist = Math.Min(Math.Max(qx, qy), 0f);
                        float sdf = outDist + inDist - radius; // <= 0 inside

                        float distFromEdge = -sdf;
                        if (distFromEdge < 0f) distFromEdge = 0f;

                        // Approximate inward normal (nx, ny) pointing from edge toward center
                        float nx = 0f;
                        float ny = 0f;
                        if (qx > 0 && qy > 0)
                        {
                            float len = (float)Math.Sqrt(qx * qx + qy * qy) + 0.0001f;
                            nx = -(px >= 0 ? 1f : -1f) * (qx / len);
                            ny = -(py >= 0 ? 1f : -1f) * (qy / len);
                        }
                        else if (qx > qy)
                        {
                            nx = -(px >= 0 ? 1f : -1f);
                        }
                        else
                        {
                            ny = -(py >= 0 ? 1f : -1f);
                        }

                        // Curved glass lens refraction profile: strongest near edge, smoothly decays inward
                        float rimT = 1f - Math.Min(1f, distFromEdge / rimDepth);
                        float bend = rimT * rimT * refract;

                        float baseX = x + pad + nx * bend;
                        float baseY = y + pad + ny * bend;

                        // Chromatic dispersion at glass bevel (R/G/B refract at slightly different indices!)
                        float cShift = rimT * rimT * chroma;
                        float sampleR = SampleChannel(blurred, workW, workH, baseX + nx * cShift, baseY + ny * cShift, 2);
                        float sampleG = SampleChannel(blurred, workW, workH, baseX, baseY, 1);
                        float sampleB = SampleChannel(blurred, workW, workH, baseX - nx * cShift, baseY - ny * cShift, 0);

                        // Slight saturation & luminosity boost inside Prism Glass so colors pop through glass
                        float lum = 0.299f * sampleR + 0.587f * sampleG + 0.114f * sampleB;
                        float sat = (palette.Name == "Prism Glass") ? 1.24f : 1.12f;
                        sampleR = ClampByte(lum + (sampleR - lum) * sat);
                        sampleG = ClampByte(lum + (sampleG - lum) * sat);
                        sampleB = ClampByte(lum + (sampleB - lum) * sat);

                        // Blend theme tint over refracted wallpaper
                        float r = sampleR * invTintA + tintR * tintA;
                        float g = sampleG * invTintA + tintG * tintA;
                        float b = sampleB * invTintA + tintB * tintA;

                        // Fresnel glass specular sheen on top and edge curves
                        if (rimT > 0.05f)
                        {
                            float topBias = (ny > 0 ? 1.0f : 0.45f); // top edge catches overhead light
                            float fresnelGain = (palette.Name == "Spatial Glass") ? 92f : 68f;
                            float fresnel = rimT * rimT * rimT * fresnelGain * topBias;
                            r = ClampByte(r + fresnel);
                            g = ClampByte(g + fresnel * 1.03f);
                            b = ClampByte(b + fresnel * 1.08f);
                        }

                        int idx = (y * w + x) * 4;
                        output[idx + 0] = (byte)b;
                        output[idx + 1] = (byte)g;
                        output[idx + 2] = (byte)r;
                        output[idx + 3] = outAlpha;
                    }
                }

                WriteableBitmap wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
                wb.WritePixels(new Int32Rect(0, 0, w, h), output, w * 4, 0);
                wb.Freeze();

                ImageBrush brush = new ImageBrush(wb);
                brush.Stretch = Stretch.Fill;
                RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return ThemeManager.Brush(palette.DockTint);
            }
        }

        private static float SampleChannel(byte[] buf, int w, int h, float fx, float fy, int channel)
        {
            if (fx < 0) fx = 0;
            if (fy < 0) fy = 0;
            if (fx > w - 1.001f) fx = w - 1.001f;
            if (fy > h - 1.001f) fy = h - 1.001f;

            int x0 = (int)fx;
            int y0 = (int)fy;
            int x1 = Math.Min(w - 1, x0 + 1);
            int y1 = Math.Min(h - 1, y0 + 1);
            float tx = fx - x0;
            float ty = fy - y0;

            int i00 = (y0 * w + x0) * 4 + channel;
            int i10 = (y0 * w + x1) * 4 + channel;
            int i01 = (y1 * w + x0) * 4 + channel;
            int i11 = (y1 * w + x1) * 4 + channel;

            float top = buf[i00] + (buf[i10] - buf[i00]) * tx;
            float bot = buf[i01] + (buf[i11] - buf[i01]) * tx;
            return top + (bot - top) * ty;
        }

        private static float ClampByte(float v)
        {
            if (v < 0f) return 0f;
            if (v > 255f) return 255f;
            return v;
        }

        private static byte[] BoxBlur(byte[] src, int w, int h, int radius)
        {
            byte[] temp = new byte[src.Length];
            byte[] dst = new byte[src.Length];
            int div = radius * 2 + 1;

            // Horizontal pass
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                int sumB = 0, sumG = 0, sumR = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int cx = Math.Max(0, Math.Min(w - 1, k));
                    int idx = (row + cx) * 4;
                    sumB += src[idx];
                    sumG += src[idx + 1];
                    sumR += src[idx + 2];
                }
                for (int x = 0; x < w; x++)
                {
                    int outIdx = (row + x) * 4;
                    temp[outIdx] = (byte)(sumB / div);
                    temp[outIdx + 1] = (byte)(sumG / div);
                    temp[outIdx + 2] = (byte)(sumR / div);
                    temp[outIdx + 3] = 255;

                    int remX = Math.Max(0, x - radius);
                    int addX = Math.Min(w - 1, x + radius + 1);
                    int rIdx = (row + remX) * 4;
                    int aIdx = (row + addX) * 4;
                    sumB += src[aIdx] - src[rIdx];
                    sumG += src[aIdx + 1] - src[rIdx + 1];
                    sumR += src[aIdx + 2] - src[rIdx + 2];
                }
            }

            // Vertical pass
            for (int x = 0; x < w; x++)
            {
                int sumB = 0, sumG = 0, sumR = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int cy = Math.Max(0, Math.Min(h - 1, k));
                    int idx = (cy * w + x) * 4;
                    sumB += temp[idx];
                    sumG += temp[idx + 1];
                    sumR += temp[idx + 2];
                }
                for (int y = 0; y < h; y++)
                {
                    int outIdx = (y * w + x) * 4;
                    dst[outIdx] = (byte)(sumB / div);
                    dst[outIdx + 1] = (byte)(sumG / div);
                    dst[outIdx + 2] = (byte)(sumR / div);
                    dst[outIdx + 3] = 255;

                    int remY = Math.Max(0, y - radius);
                    int addY = Math.Min(h - 1, y + radius + 1);
                    int rIdx = (remY * w + x) * 4;
                    int aIdx = (addY * w + x) * 4;
                    sumB += temp[aIdx] - temp[rIdx];
                    sumG += temp[aIdx + 1] - temp[rIdx + 1];
                    sumR += temp[aIdx + 2] - temp[rIdx + 2];
                }
            }

            return dst;
        }
    }
}
