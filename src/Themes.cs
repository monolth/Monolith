using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Monolith
{
    public class DockMetrics
    {
        public double BarHeight;
        public double OuterRadius;
        public double InnerRadius;
        public double BarPaddingX;
        public double BarPaddingY;
        public double ItemGap;
        public double IconTileSize;
        public double IconImageSize;
        public double CardHeight;
        public double FontTitle;
        public double FontValue;
        public double FontSub;
        public double EdgeMargin;
    }

    public class ThemePalette
    {
        public string Name;
        public bool IsLight;
        public bool UsesRefraction;
        public bool IsClear;
        public double BlurRadius;
        public double RefractionStrength;
        public double ChromaticShift;

        public Color DockTint;
        public Color DockBorderTop;
        public Color DockBorderBottom;
        public Color OrbBg;
        public Color OrbHoverBg;
        public Color OrbBorder;
        public Color CardBg;
        public Color CardHoverBg;
        public Color CardBorder;
        public Color DividerColor;
        public Color TextPrimary;
        public Color TextSecondary;
        public Color TextMuted;
        public Color Accent;
        public Color ModalBg;
        public Color ModalSurface;
        public Color ModalBorder;
        public Color TooltipBg;
        public Color TooltipText;
    }

    public static class ThemeManager
    {
        public static readonly string[] ThemeNames = new string[]
        {
            "Spatial Glass",
            "Prism Glass",
            "Aurora Glass",
            "Glacier Frost",
            "Midnight",
            "Obsidian Neon",
            "Ceramic White",
            "Clear"
        };

        public static readonly Dictionary<string, Color> Accents = new Dictionary<string, Color>
        {
            { "Sky",      Color.FromRgb(56, 189, 248) },
            { "Blue",     Color.FromRgb(59, 130, 246) },
            { "Indigo",   Color.FromRgb(99, 102, 241) },
            { "Violet",   Color.FromRgb(139, 92, 246) },
            { "Purple",   Color.FromRgb(168, 85, 247) },
            { "Magenta",  Color.FromRgb(217, 70, 239) },
            { "Pink",     Color.FromRgb(236, 72, 153) },
            { "Rose",     Color.FromRgb(244, 63, 94) },
            { "Red",      Color.FromRgb(239, 68, 68) },
            { "Coral",    Color.FromRgb(251, 113, 133) },
            { "Orange",   Color.FromRgb(249, 115, 22) },
            { "Amber",    Color.FromRgb(245, 158, 11) },
            { "Gold",     Color.FromRgb(234, 179, 8) },
            { "Emerald",  Color.FromRgb(16, 185, 129) },
            { "Green",    Color.FromRgb(34, 197, 94) },
            { "Teal",     Color.FromRgb(20, 184, 166) },
            { "Cyan",     Color.FromRgb(6, 182, 212) },
            { "Graphite", Color.FromRgb(148, 163, 184) }
        };

        public static readonly Dictionary<string, Color> DockTints = new Dictionary<string, Color>
        {
            { "Sapphire", Color.FromRgb(30, 64, 175) },
            { "Royal",    Color.FromRgb(109, 40, 217) },
            { "Magenta",  Color.FromRgb(162, 28, 175) },
            { "Rose",     Color.FromRgb(190, 18, 60) },
            { "Crimson",  Color.FromRgb(185, 28, 28) },
            { "Sunset",   Color.FromRgb(194, 65, 12) },
            { "Amber",    Color.FromRgb(180, 83, 9) },
            { "Emerald",  Color.FromRgb(4, 120, 87) },
            { "Ocean",    Color.FromRgb(14, 116, 144) },
            { "Slate",    Color.FromRgb(30, 41, 59) }
        };

        public static bool TryParseHexColor(string hex, out Color color)
        {
            color = Colors.Transparent;
            if (string.IsNullOrEmpty(hex)) return false;
            string s = hex.Trim();
            if (!s.StartsWith("#")) s = "#" + s;
            if (s.Length != 7) return false;
            try
            {
                color = (Color)ColorConverter.ConvertFromString(s);
                return true;
            }
            catch { return false; }
        }

        public static Color GetAccentColor(string name, string customHex)
        {
            Color custom;
            if (name == "Custom" && TryParseHexColor(customHex, out custom))
                return custom;
            Color c;
            if (!string.IsNullOrEmpty(name) && Accents.TryGetValue(name, out c))
                return c;
            return Accents["Blue"];
        }

        public static Color GetAccentColor(string name)
        {
            return GetAccentColor(name, null);
        }

        /// <summary>
        /// Spatial stadium-pill proportions: OuterRadius = BarHeight / 2 (true full-pill)
        /// paired with concentric inner pill widget cards & circular app orbs.
        /// </summary>
        public static DockMetrics GetMetrics(string sizeTier)
        {
            if (sizeTier == "S")
            {
                return new DockMetrics
                {
                    BarHeight = 56,
                    OuterRadius = 28,
                    InnerRadius = 21,
                    BarPaddingX = 11,
                    BarPaddingY = 6,
                    ItemGap = 6,
                    IconTileSize = 40,
                    IconImageSize = 23,
                    CardHeight = 42,
                    FontTitle = 9.0,
                    FontValue = 13.0,
                    FontSub = 9.2,
                    EdgeMargin = 12
                };
            }
            if (sizeTier == "L")
            {
                return new DockMetrics
                {
                    BarHeight = 76,
                    OuterRadius = 38,
                    InnerRadius = 28,
                    BarPaddingX = 15,
                    BarPaddingY = 8,
                    ItemGap = 8,
                    IconTileSize = 52,
                    IconImageSize = 30,
                    CardHeight = 58,
                    FontTitle = 10.5,
                    FontValue = 16.5,
                    FontSub = 11.0,
                    EdgeMargin = 16
                };
            }

            // Default M (66px Spatial stadium pill)
            return new DockMetrics
            {
                BarHeight = 66,
                OuterRadius = 33,
                InnerRadius = 25,
                BarPaddingX = 13,
                BarPaddingY = 7,
                ItemGap = 7,
                IconTileSize = 46,
                IconImageSize = 26,
                CardHeight = 50,
                FontTitle = 9.5,
                FontValue = 14.5,
                FontSub = 10.0,
                EdgeMargin = 14
            };
        }

        public static ThemePalette GetPalette(MonolithConfig cfg)
        {
            ThemePalette p = BuildBasePalette(cfg);
            ApplyUserDockTint(p, cfg);
            return p;
        }

        private static Color MixRgb(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                a.A,
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        private static Color Lighten(Color c, double amount)
        {
            return Color.FromArgb(
                c.A,
                (byte)Math.Min(255, Math.Round(c.R + (255 - c.R) * amount)),
                (byte)Math.Min(255, Math.Round(c.G + (255 - c.G) * amount)),
                (byte)Math.Min(255, Math.Round(c.B + (255 - c.B) * amount)));
        }

        private static Color Deepen(Color c, double factor)
        {
            return Color.FromArgb(
                c.A,
                (byte)Math.Round(c.R * factor),
                (byte)Math.Round(c.G * factor),
                (byte)Math.Round(c.B * factor));
        }

        private static void ApplyUserDockTint(ThemePalette p, MonolithConfig cfg)
        {
            if (cfg == null || string.IsNullOrEmpty(cfg.DockTintColor) || cfg.DockTintColor == "Natural")
                return;

            Color tintCol;
            if (cfg.DockTintColor == "Accent")
            {
                tintCol = p.Accent;
            }
            else if (!DockTints.TryGetValue(cfg.DockTintColor, out tintCol))
            {
                if (!TryParseHexColor(cfg.DockTintColor, out tintCol))
                    return;
            }

            double s = cfg.ColorTintStrength;
            if (s < 0.05) s = 0.05;
            if (s > 1.0) s = 1.0;

            if (p.IsLight)
            {
                // Soft pastel wash for Ceramic White
                Color pastel = Lighten(tintCol, 0.72);
                p.DockTint = MixRgb(p.DockTint, pastel, s * 0.65);
                p.CardBg = MixRgb(p.CardBg, Lighten(tintCol, 0.80), s * 0.55);
                p.OrbBg = MixRgb(p.OrbBg, Lighten(tintCol, 0.84), s * 0.45);
                p.DockBorderBottom = MixRgb(p.DockBorderBottom, tintCol, s * 0.65);
                p.CardBorder = MixRgb(p.CardBorder, tintCol, s * 0.55);
                p.ModalBg = MixRgb(p.ModalBg, Lighten(tintCol, 0.86), s * 0.40);
                p.ModalBorder = MixRgb(p.ModalBorder, tintCol, s * 0.50);
            }
            else
            {
                // Rich translucent jewel-toned Spatial glass!
                Color deep = Deepen(tintCol, 0.42);
                Color mid = Deepen(tintCol, 0.58);
                Color brightRim = Lighten(tintCol, 0.58);

                byte boostedAlpha = p.IsClear ? p.DockTint.A : (byte)Math.Min(235, p.DockTint.A + (int)(56 * s));
                Color tintedDock = MixRgb(p.DockTint, mid, Math.Min(0.92, s * 1.15));
                p.DockTint = Color.FromArgb(boostedAlpha, tintedDock.R, tintedDock.G, tintedDock.B);

                p.DockBorderTop = MixRgb(p.DockBorderTop, brightRim, s * 0.85);
                p.DockBorderBottom = MixRgb(p.DockBorderBottom, tintCol, s * 0.90);
                p.OrbBg = MixRgb(p.OrbBg, mid, s * 0.65);
                p.OrbHoverBg = MixRgb(p.OrbHoverBg, tintCol, s * 0.65);
                p.OrbBorder = MixRgb(p.OrbBorder, brightRim, s * 0.75);
                p.CardBg = MixRgb(p.CardBg, deep, s * 0.78);
                p.CardHoverBg = MixRgb(p.CardHoverBg, mid, s * 0.78);
                p.CardBorder = MixRgb(p.CardBorder, brightRim, s * 0.68);
                p.DividerColor = MixRgb(p.DividerColor, brightRim, s * 0.65);
                p.ModalBg = MixRgb(p.ModalBg, Deepen(tintCol, 0.24), s * 0.72);
                p.ModalSurface = MixRgb(p.ModalSurface, Deepen(tintCol, 0.34), s * 0.72);
                p.ModalBorder = MixRgb(p.ModalBorder, brightRim, s * 0.65);
            }
        }

        private static ThemePalette BuildBasePalette(MonolithConfig cfg)
        {
            string name = (cfg != null && !string.IsNullOrEmpty(cfg.Theme)) ? cfg.Theme : "Spatial Glass";
            Color accent = GetAccentColor(cfg != null ? cfg.Accent : "Blue", cfg != null ? cfg.CustomAccentHex : null);
            double intensity = (cfg != null) ? cfg.GlassIntensity : 0.88;
            if (intensity < 0.25) intensity = 0.25;
            if (intensity > 1.0) intensity = 1.0;

            ThemePalette p = new ThemePalette();
            p.Name = name;
            p.Accent = accent;
            p.TooltipBg = Color.FromArgb(236, 16, 19, 28);
            p.TooltipText = Color.FromRgb(248, 250, 255);

            if (name == "Ceramic White" || name == "White")
            {
                p.Name = "Ceramic White";
                p.IsLight = true;
                p.UsesRefraction = false;
                p.DockTint = Color.FromArgb(242, 247, 249, 253);
                p.DockBorderTop = Color.FromArgb(245, 255, 255, 255);
                p.DockBorderBottom = Color.FromArgb(72, 15, 23, 42);
                p.OrbBg = Color.FromArgb(230, 255, 255, 255);
                p.OrbHoverBg = Color.FromArgb(255, 255, 255, 255);
                p.OrbBorder = Color.FromArgb(46, 15, 23, 42);
                p.CardBg = Color.FromArgb(228, 234, 239, 247);
                p.CardHoverBg = Color.FromArgb(250, 224, 231, 242);
                p.CardBorder = Color.FromArgb(42, 15, 23, 42);
                p.DividerColor = Color.FromArgb(46, 15, 23, 42);
                p.TextPrimary = Color.FromRgb(18, 22, 30);
                p.TextSecondary = Color.FromRgb(68, 82, 102);
                p.TextMuted = Color.FromRgb(112, 126, 146);
                p.ModalBg = Color.FromArgb(248, 247, 249, 252);
                p.ModalSurface = Color.FromArgb(255, 255, 255, 255);
                p.ModalBorder = Color.FromArgb(46, 15, 23, 42);
                p.TooltipBg = Color.FromArgb(246, 255, 255, 255);
                p.TooltipText = Color.FromRgb(18, 22, 30);
                return p;
            }

            if (name == "Obsidian Neon" || name == "Black")
            {
                p.Name = "Obsidian Neon";
                p.IsLight = false;
                p.UsesRefraction = false;
                p.DockTint = Color.FromArgb(244, 11, 13, 20);
                p.DockBorderTop = Color.FromArgb(200, accent.R, accent.G, accent.B);
                p.DockBorderBottom = Color.FromArgb(92, accent.R, accent.G, accent.B);
                p.OrbBg = Color.FromArgb(220, 24, 28, 40);
                p.OrbHoverBg = Color.FromArgb(245, 36, 42, 62);
                p.OrbBorder = Color.FromArgb(88, accent.R, accent.G, accent.B);
                p.CardBg = Color.FromArgb(234, 20, 23, 33);
                p.CardHoverBg = Color.FromArgb(248, 30, 35, 52);
                p.CardBorder = Color.FromArgb(76, accent.R, accent.G, accent.B);
                p.DividerColor = Color.FromArgb(80, accent.R, accent.G, accent.B);
                p.TextPrimary = Color.FromRgb(248, 250, 252);
                p.TextSecondary = Color.FromRgb(196, 206, 224);
                p.TextMuted = Color.FromRgb(136, 148, 170);
                p.ModalBg = Color.FromArgb(248, 12, 14, 21);
                p.ModalSurface = Color.FromArgb(255, 22, 25, 36);
                p.ModalBorder = Color.FromArgb(100, accent.R, accent.G, accent.B);
                return p;
            }

            if (name == "Midnight")
            {
                p.IsLight = false;
                p.UsesRefraction = true;
                p.BlurRadius = 15;
                p.RefractionStrength = 5.5 * intensity;
                p.ChromaticShift = 0.9 * intensity;
                p.DockTint = Color.FromArgb(205, 13, 20, 38);
                p.DockBorderTop = Color.FromArgb(115, 165, 202, 255);
                p.DockBorderBottom = Color.FromArgb(48, 96, 140, 230);
                p.OrbBg = Color.FromArgb(140, 30, 42, 74);
                p.OrbHoverBg = Color.FromArgb(195, 44, 62, 106);
                p.OrbBorder = Color.FromArgb(80, 175, 208, 255);
                p.CardBg = Color.FromArgb(158, 22, 32, 58);
                p.CardHoverBg = Color.FromArgb(202, 34, 48, 84);
                p.CardBorder = Color.FromArgb(62, 165, 196, 255);
                p.DividerColor = Color.FromArgb(66, 165, 196, 255);
                p.TextPrimary = Color.FromRgb(244, 248, 255);
                p.TextSecondary = Color.FromRgb(180, 200, 232);
                p.TextMuted = Color.FromRgb(126, 148, 184);
                p.ModalBg = Color.FromArgb(244, 13, 19, 36);
                p.ModalSurface = Color.FromArgb(242, 23, 32, 56);
                p.ModalBorder = Color.FromArgb(78, 155, 192, 255);
                return p;
            }

            if (name == "Aurora Glass")
            {
                p.IsLight = false;
                p.UsesRefraction = true;
                p.BlurRadius = 15;
                p.RefractionStrength = 11.0 * intensity;
                p.ChromaticShift = 3.2 * intensity;
                p.DockTint = Color.FromArgb(124, 22, 18, 48);
                p.DockBorderTop = Color.FromArgb(195, 140, 218, 255);
                p.DockBorderBottom = Color.FromArgb(120, 196, 138, 255);
                p.OrbBg = Color.FromArgb(72, 255, 255, 255);
                p.OrbHoverBg = Color.FromArgb(115, 255, 255, 255);
                p.OrbBorder = Color.FromArgb(115, 196, 212, 255);
                p.CardBg = Color.FromArgb(118, 18, 16, 42);
                p.CardHoverBg = Color.FromArgb(164, 34, 30, 72);
                p.CardBorder = Color.FromArgb(95, 175, 192, 255);
                p.DividerColor = Color.FromArgb(95, 175, 192, 255);
                p.TextPrimary = Color.FromRgb(252, 253, 255);
                p.TextSecondary = Color.FromRgb(212, 222, 248);
                p.TextMuted = Color.FromRgb(156, 168, 210);
                p.ModalBg = Color.FromArgb(244, 16, 15, 34);
                p.ModalSurface = Color.FromArgb(242, 28, 26, 56);
                p.ModalBorder = Color.FromArgb(105, 175, 192, 255);
                return p;
            }

            if (name == "Glacier Frost")
            {
                p.Name = "Glacier Frost";
                p.IsLight = false;
                p.UsesRefraction = true;
                p.BlurRadius = 18;
                p.RefractionStrength = 7.5 * intensity;
                p.ChromaticShift = 1.4 * intensity;
                p.DockTint = Color.FromArgb(132, 34, 48, 74);
                p.DockBorderTop = Color.FromArgb(175, 232, 244, 255);
                p.DockBorderBottom = Color.FromArgb(74, 184, 216, 255);
                p.OrbBg = Color.FromArgb(58, 235, 246, 255);
                p.OrbHoverBg = Color.FromArgb(100, 235, 246, 255);
                p.OrbBorder = Color.FromArgb(98, 225, 242, 255);
                p.CardBg = Color.FromArgb(108, 18, 28, 48);
                p.CardHoverBg = Color.FromArgb(152, 32, 46, 74);
                p.CardBorder = Color.FromArgb(84, 225, 240, 255);
                p.DividerColor = Color.FromArgb(86, 225, 240, 255);
                p.TextPrimary = Color.FromRgb(250, 252, 255);
                p.TextSecondary = Color.FromRgb(212, 226, 244);
                p.TextMuted = Color.FromRgb(162, 182, 208);
                p.ModalBg = Color.FromArgb(242, 18, 24, 38);
                p.ModalSurface = Color.FromArgb(235, 30, 40, 62);
                p.ModalBorder = Color.FromArgb(90, 214, 234, 255);
                return p;
            }

            if (name == "Prism Glass")
            {
                p.Name = "Prism Glass";
                p.IsLight = false;
                p.UsesRefraction = true;
                p.BlurRadius = 11;
                p.RefractionStrength = 13.0 * intensity;
                p.ChromaticShift = 2.6 * intensity;
                p.DockTint = Color.FromArgb((byte)(86 + (1.0 - intensity) * 42), 18, 20, 28);
                p.DockBorderTop = Color.FromArgb(195, 255, 255, 255);
                p.DockBorderBottom = Color.FromArgb(95, 255, 255, 255);
                p.OrbBg = Color.FromArgb(54, 255, 255, 255);
                p.OrbHoverBg = Color.FromArgb(96, 255, 255, 255);
                p.OrbBorder = Color.FromArgb(92, 255, 255, 255);
                p.CardBg = Color.FromArgb(124, 15, 18, 27);
                p.CardHoverBg = Color.FromArgb(166, 28, 33, 46);
                p.CardBorder = Color.FromArgb(82, 255, 255, 255);
                p.DividerColor = Color.FromArgb(86, 255, 255, 255);
                p.TextPrimary = Color.FromRgb(252, 253, 255);
                p.TextSecondary = Color.FromRgb(218, 224, 236);
                p.TextMuted = Color.FromRgb(168, 178, 196);
                p.ModalBg = Color.FromArgb(242, 18, 20, 27);
                p.ModalSurface = Color.FromArgb(240, 29, 32, 42);
                p.ModalBorder = Color.FromArgb(88, 255, 255, 255);
                return p;
            }

            if (name == "Clear")
            {
                string cardMode = (cfg != null && !string.IsNullOrEmpty(cfg.ClearCardStyle)) ? cfg.ClearCardStyle : "Glass";
                p.IsClear = true;
                p.UsesRefraction = false;
                p.DockTint = Color.FromArgb(26, 255, 255, 255);
                p.DockBorderTop = Color.FromArgb(98, 255, 255, 255);
                p.DockBorderBottom = Color.FromArgb(48, 255, 255, 255);
                p.DividerColor = Color.FromArgb(72, 255, 255, 255);
                p.ModalBg = Color.FromArgb(242, 18, 20, 28);
                p.ModalSurface = Color.FromArgb(242, 30, 33, 44);
                p.ModalBorder = Color.FromArgb(72, 255, 255, 255);

                if (cardMode == "White")
                {
                    p.IsLight = true;
                    p.OrbBg = Color.FromArgb(236, 252, 253, 255);
                    p.OrbHoverBg = Color.FromArgb(255, 255, 255, 255);
                    p.OrbBorder = Color.FromArgb(60, 15, 23, 42);
                    p.CardBg = Color.FromArgb(236, 250, 251, 254);
                    p.CardHoverBg = Color.FromArgb(250, 238, 242, 248);
                    p.CardBorder = Color.FromArgb(55, 15, 23, 42);
                    p.TextPrimary = Color.FromRgb(18, 22, 30);
                    p.TextSecondary = Color.FromRgb(68, 82, 102);
                    p.TextMuted = Color.FromRgb(110, 124, 144);
                }
                else if (cardMode == "Black")
                {
                    p.IsLight = false;
                    p.OrbBg = Color.FromArgb(230, 22, 24, 30);
                    p.OrbHoverBg = Color.FromArgb(248, 36, 39, 48);
                    p.OrbBorder = Color.FromArgb(65, 255, 255, 255);
                    p.CardBg = Color.FromArgb(232, 18, 19, 24);
                    p.CardHoverBg = Color.FromArgb(245, 32, 34, 42);
                    p.CardBorder = Color.FromArgb(55, 255, 255, 255);
                    p.TextPrimary = Color.FromRgb(248, 250, 252);
                    p.TextSecondary = Color.FromRgb(192, 200, 214);
                    p.TextMuted = Color.FromRgb(142, 152, 168);
                }
                else
                {
                    p.IsLight = false;
                    p.OrbBg = Color.FromArgb(132, 22, 26, 38);
                    p.OrbHoverBg = Color.FromArgb(182, 36, 42, 62);
                    p.OrbBorder = Color.FromArgb(98, 255, 255, 255);
                    p.CardBg = Color.FromArgb(146, 18, 22, 32);
                    p.CardHoverBg = Color.FromArgb(186, 30, 36, 52);
                    p.CardBorder = Color.FromArgb(92, 255, 255, 255);
                    p.TextPrimary = Color.FromRgb(250, 252, 255);
                    p.TextSecondary = Color.FromRgb(215, 222, 235);
                    p.TextMuted = Color.FromRgb(168, 178, 196);
                }
                return p;
            }

            // DEFAULT: Spatial Glass (Crystalline Depth & Luminous Specular Rim)
            p.Name = "Spatial Glass";
            p.IsLight = false;
            p.UsesRefraction = true;
            p.BlurRadius = 15;
            p.RefractionStrength = 11.5 * intensity;
            p.ChromaticShift = 2.0 * intensity;
            p.DockTint = Color.FromArgb((byte)(68 + (1.0 - intensity) * 40), 225, 235, 255); // Luminous frosted crystal tint!
            p.DockBorderTop = Color.FromArgb(228, 255, 255, 255);
            p.DockBorderBottom = Color.FromArgb(105, 210, 230, 255);
            p.OrbBg = Color.FromArgb(62, 255, 255, 255);
            p.OrbHoverBg = Color.FromArgb(108, 255, 255, 255);
            p.OrbBorder = Color.FromArgb(128, 255, 255, 255);
            p.CardBg = Color.FromArgb(138, 12, 16, 26);
            p.CardHoverBg = Color.FromArgb(174, 22, 28, 44);
            p.CardBorder = Color.FromArgb(104, 255, 255, 255);
            p.DividerColor = Color.FromArgb(110, 255, 255, 255);
            p.TextPrimary = Color.FromRgb(253, 254, 255);
            p.TextSecondary = Color.FromRgb(220, 228, 242);
            p.TextMuted = Color.FromRgb(172, 184, 206);
            p.ModalBg = Color.FromArgb(242, 16, 19, 28);
            p.ModalSurface = Color.FromArgb(238, 27, 32, 46);
            p.ModalBorder = Color.FromArgb(110, 255, 255, 255);
            return p;
        }

        public static SolidColorBrush Brush(Color c)
        {
            SolidColorBrush b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public static Brush CreateDockBorderBrush(ThemePalette p)
        {
            LinearGradientBrush lg = new LinearGradientBrush();
            lg.StartPoint = new Point(0, 0);
            lg.EndPoint = new Point(0, 1);
            lg.GradientStops.Add(new GradientStop(p.DockBorderTop, 0.0));
            lg.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(p.DockBorderTop.A * 0.45), p.DockBorderTop.R, p.DockBorderTop.G, p.DockBorderTop.B), 0.45));
            lg.GradientStops.Add(new GradientStop(p.DockBorderBottom, 1.0));
            lg.Freeze();
            return lg;
        }

        /// <summary>
        /// 4-layer Spatial Optical Glass Orb Body: radial top light scatter -> clear hollow core -> bottom bounce rim.
        /// </summary>
        public static Brush CreateOrbBodyBrush(ThemePalette p, bool hovered)
        {
            RadialGradientBrush rg = new RadialGradientBrush();
            rg.Center = new Point(0.5, 0.24);
            rg.GradientOrigin = new Point(0.5, 0.14);
            rg.RadiusX = 0.72;
            rg.RadiusY = 0.78;

            if (p.IsLight)
            {
                byte topA = hovered ? (byte)250 : (byte)230;
                byte midA = hovered ? (byte)220 : (byte)175;
                byte botA = hovered ? (byte)235 : (byte)205;
                rg.GradientStops.Add(new GradientStop(Color.FromArgb(topA, 255, 255, 255), 0.0));
                rg.GradientStops.Add(new GradientStop(Color.FromArgb(midA, p.OrbBg.R, p.OrbBg.G, p.OrbBg.B), 0.62));
                rg.GradientStops.Add(new GradientStop(Color.FromArgb(botA, 225, 232, 245), 1.0));
            }
            else
            {
                Color baseCol = hovered ? p.OrbHoverBg : p.OrbBg;
                Color topLight = Lighten(baseCol, 0.78);
                byte topA = hovered ? (byte)135 : (byte)86;
                byte coreA = hovered ? (byte)76 : (byte)36;
                byte rimA = hovered ? (byte)112 : (byte)64;

                rg.GradientStops.Add(new GradientStop(Color.FromArgb(topA, topLight.R, topLight.G, topLight.B), 0.0));
                rg.GradientStops.Add(new GradientStop(Color.FromArgb(coreA, baseCol.R, baseCol.G, baseCol.B), 0.58));
                rg.GradientStops.Add(new GradientStop(Color.FromArgb(rimA, topLight.R, topLight.G, topLight.B), 1.0));
            }
            rg.Freeze();
            return rg;
        }

        /// <summary>
        /// Directional specular rim stroke for 3D curved glass orbs (bright top-left light -> translucent middle -> bounce bottom).
        /// </summary>
        public static Brush CreateOrbRimBrush(ThemePalette p, bool hovered)
        {
            LinearGradientBrush lg = new LinearGradientBrush();
            lg.StartPoint = new Point(0.15, 0.0);
            lg.EndPoint = new Point(0.85, 1.0);

            if (p.IsLight)
            {
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(240, 255, 255, 255), 0.0));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(45, 15, 23, 42), 0.52));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(hovered ? (byte)110 : (byte)72, 15, 23, 42), 1.0));
            }
            else
            {
                Color topC = hovered ? Color.FromArgb(245, 255, 255, 255) : Color.FromArgb(215, 255, 255, 255);
                Color midC = Color.FromArgb(hovered ? (byte)78 : (byte)36, p.OrbBorder.R, p.OrbBorder.G, p.OrbBorder.B);
                Color botC = Color.FromArgb(hovered ? (byte)165 : (byte)108, p.OrbBorder.R, p.OrbBorder.G, p.OrbBorder.B);
                lg.GradientStops.Add(new GradientStop(topC, 0.0));
                lg.GradientStops.Add(new GradientStop(midC, 0.52));
                lg.GradientStops.Add(new GradientStop(botC, 1.0));
            }
            lg.Freeze();
            return lg;
        }

        /// <summary>
        /// Curved top specular gloss crescent inside glass orbs.
        /// </summary>
        public static Brush CreateOrbSpecularCrescentBrush(ThemePalette p)
        {
            LinearGradientBrush lg = new LinearGradientBrush();
            lg.StartPoint = new Point(0.5, 0.0);
            lg.EndPoint = new Point(0.5, 1.0);
            byte peak = p.IsLight ? (byte)155 : (byte)96;
            lg.GradientStops.Add(new GradientStop(Color.FromArgb(peak, 255, 255, 255), 0.0));
            lg.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(peak / 4), 255, 255, 255), 0.52));
            lg.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));
            lg.Freeze();
            return lg;
        }

        /// <summary>
        /// Subtle concave bottom inner depth shadow for physical 3D glass lens thickness.
        /// </summary>
        public static Brush CreateOrbBottomShadeBrush(ThemePalette p)
        {
            LinearGradientBrush lg = new LinearGradientBrush();
            lg.StartPoint = new Point(0.5, 0.0);
            lg.EndPoint = new Point(0.5, 1.0);
            lg.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.0));
            lg.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.62));
            lg.GradientStops.Add(new GradientStop(Color.FromArgb(p.IsLight ? (byte)28 : (byte)64, 0, 0, 0), 1.0));
            lg.Freeze();
            return lg;
        }

        public static Brush CreateSpecularHighlightBrush(ThemePalette p)
        {
            LinearGradientBrush lg = new LinearGradientBrush();
            lg.StartPoint = new Point(0, 0);
            lg.EndPoint = new Point(0, 1);
            if (p.IsLight)
            {
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(130, 255, 255, 255), 0.0));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.45));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(14, 0, 0, 0), 1.0));
            }
            else if (p.Name == "Spatial Glass")
            {
                // Signature crystalline top crescent sheen
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(105, 255, 255, 255), 0.0));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(34, 240, 248, 255), 0.22));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(6, 15, 20, 35), 0.56));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(38, 225, 240, 255), 1.0));
            }
            else if (p.Name == "Prism Glass" || p.Name == "Aurora Glass")
            {
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(78, 255, 255, 255), 0.0));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(18, 255, 255, 255), 0.24));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.55));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(28, 255, 255, 255), 1.0));
            }
            else if (p.Name == "Glacier Frost")
            {
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(66, 220, 238, 255), 0.0));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(10, 200, 225, 255), 0.35));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(20, 180, 210, 255), 1.0));
            }
            else
            {
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(26, 255, 255, 255), 0.0));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.5));
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(12, 255, 255, 255), 1.0));
            }
            lg.Freeze();
            return lg;
        }
    }
}
