using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace Monolith
{
    public class IconStyleDef
    {
        public string Id;
        public string NameTr;
        public string NameEn;
    }

    /// <summary>
    /// Builds all 18 selectable Monolith icon glass frame styles with smooth hover transitions.
    /// </summary>
    public static class IconStyleFactory
    {
        public static readonly List<IconStyleDef> AllStyles = new List<IconStyleDef>
        {
            new IconStyleDef { Id = "Squircle",       NameTr = "Yumuşak Kare",     NameEn = "Squircle Glass" },
            new IconStyleDef { Id = "Circle",         NameTr = "Yuvarlak Küre",    NameEn = "Circle Orb" },
            new IconStyleDef { Id = "DarkCrystal",    NameTr = "Koyu Kristal",     NameEn = "Dark Crystal" },
            new IconStyleDef { Id = "Borderless",     NameTr = "Çerçevesiz",       NameEn = "Borderless" },
            new IconStyleDef { Id = "SharpPrism",     NameTr = "Keskin Prizma",    NameEn = "Sharp Prism" },
            new IconStyleDef { Id = "GlowPedestal",   NameTr = "Işıklı Taban",     NameEn = "Glow Pedestal" },
            new IconStyleDef { Id = "NeonRing",       NameTr = "Neon Halka",       NameEn = "Neon Ring" },
            new IconStyleDef { Id = "DoubleGlass",    NameTr = "Çift Katman",      NameEn = "Double Glass" },
            new IconStyleDef { Id = "WideCapsule",    NameTr = "Yatay Hap",        NameEn = "Wide Capsule" },
            new IconStyleDef { Id = "AmbientAura",    NameTr = "Ortam Halesi",     NameEn = "Ambient Aura" },
            new IconStyleDef { Id = "HudBrackets",    NameTr = "Köşe Çentikli",    NameEn = "HUD Brackets" },
            new IconStyleDef { Id = "DiamondCrystal", NameTr = "Elmas Kristal",    NameEn = "Diamond Crystal" },
            new IconStyleDef { Id = "TitaniumChrome", NameTr = "Titanyum Krom",    NameEn = "Titanium Chrome" },
            new IconStyleDef { Id = "HexagonCrystal", NameTr = "Altıgen Kristal",  NameEn = "Hexagon Crystal" },
            new IconStyleDef { Id = "Holographic",    NameTr = "Holografik Cam",   NameEn = "Holographic" },
            new IconStyleDef { Id = "OrganicLeaf",    NameTr = "Organik Yaprak",   NameEn = "Organic Leaf" },
            new IconStyleDef { Id = "NeumorphicInset",NameTr = "Oyuk Cam Tuş",     NameEn = "Neumorphic Inset" },
            new IconStyleDef { Id = "DottedTech",     NameTr = "Kesikli Teknik",   NameEn = "Dotted Tech" }
        };

        private class StyleParts
        {
            public string StyleId;
            public double Size;
            public Grid ContentHost;
            public Border InnerShade;
            public Border InnerBox;
            public Ellipse AuraEllipse;
            public Polygon HexPoly;
            public Rectangle DottedRect;
            public Border DiamondBox;
            public List<Border> HudCorners;
        }

        public static Border CreateTile(string styleId, double sz, ThemePalette p)
        {
            if (string.IsNullOrEmpty(styleId)) styleId = "Squircle";

            TransformGroup tg = new TransformGroup();
            tg.Children.Add(new ScaleTransform(1.0, 1.0));
            tg.Children.Add(new TranslateTransform(0.0, 0.0));

            double width = (styleId == "WideCapsule") ? Math.Round(sz * 1.18) : sz;
            double height = (styleId == "WideCapsule") ? Math.Round(sz * 0.88) : sz;

            Border outer = new Border
            {
                Width = width,
                Height = height,
                Margin = new Thickness(3, 0, 3, 0),
                Cursor = Cursors.Hand,
                SnapsToDevicePixels = true,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = tg
            };

            Grid layers = new Grid();
            StyleParts parts = new StyleParts { StyleId = styleId, Size = sz };

            if (styleId == "Circle" || styleId == "Squircle" || styleId == "SharpPrism" || styleId == "WideCapsule" || styleId == "OrganicLeaf")
            {
                CornerRadius cr = GetCornerRadius(styleId, sz, height, false);
                outer.CornerRadius = cr;
                outer.BorderThickness = new Thickness(1.15);
                outer.ClipToBounds = true;

                parts.InnerShade = new Border
                {
                    CornerRadius = cr,
                    Background = ThemeManager.CreateOrbBottomShadeBrush(p),
                    IsHitTestVisible = false
                };
                layers.Children.Add(parts.InnerShade);
                layers.Children.Add(new Ellipse
                {
                    Width = width * 0.68,
                    Height = height * 0.28,
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0),
                    Fill = ThemeManager.CreateOrbSpecularCrescentBrush(p),
                    IsHitTestVisible = false
                });
            }
            else if (styleId == "DarkCrystal")
            {
                CornerRadius cr = new CornerRadius(Math.Round(sz * 0.30));
                outer.CornerRadius = cr;
                outer.BorderThickness = new Thickness(1.2);
                outer.ClipToBounds = true;

                layers.Children.Add(new Ellipse
                {
                    Width = width * 0.66,
                    Height = height * 0.26,
                    VerticalAlignment = VerticalAlignment.Top,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0),
                    Fill = ThemeManager.CreateOrbSpecularCrescentBrush(p),
                    Opacity = 0.72,
                    IsHitTestVisible = false
                });
            }
            else if (styleId == "Borderless")
            {
                outer.CornerRadius = new CornerRadius(Math.Round(sz * 0.30));
                outer.BorderThickness = new Thickness(1);
            }
            else if (styleId == "GlowPedestal")
            {
                outer.CornerRadius = new CornerRadius(Math.Round(sz * 0.26));
                outer.BorderThickness = new Thickness(0, 0, 0, 2.2);
            }
            else if (styleId == "NeonRing")
            {
                outer.CornerRadius = new CornerRadius(Math.Round(sz * 0.30));
                outer.BorderThickness = new Thickness(1.5);
                outer.Effect = new DropShadowEffect
                {
                    BlurRadius = 10,
                    ShadowDepth = 0,
                    Opacity = 0.55,
                    Color = p.Accent
                };
            }
            else if (styleId == "DoubleGlass")
            {
                outer.CornerRadius = new CornerRadius(Math.Round(sz * 0.32));
                outer.BorderThickness = new Thickness(1);
                outer.Padding = new Thickness(3);
                parts.InnerBox = new Border
                {
                    CornerRadius = new CornerRadius(Math.Round(sz * 0.22)),
                    BorderThickness = new Thickness(1),
                    IsHitTestVisible = false
                };
                layers.Children.Add(parts.InnerBox);
            }
            else if (styleId == "AmbientAura")
            {
                outer.CornerRadius = new CornerRadius(sz / 2.0);
                outer.Background = Brushes.Transparent;
                parts.AuraEllipse = new Ellipse
                {
                    Width = sz * 0.78,
                    Height = sz * 0.78,
                    Fill = ThemeManager.Brush(Color.FromArgb(135, p.Accent.R, p.Accent.G, p.Accent.B)),
                    Effect = new BlurEffect { Radius = 14 },
                    IsHitTestVisible = false
                };
                layers.Children.Add(parts.AuraEllipse);
            }
            else if (styleId == "HudBrackets")
            {
                outer.CornerRadius = new CornerRadius(6);
                parts.HudCorners = new List<Border>();
                double bLen = Math.Round(sz * 0.20);
                parts.HudCorners.Add(MakeHudCorner(bLen, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(1.8, 1.8, 0, 0), new CornerRadius(3, 0, 0, 0)));
                parts.HudCorners.Add(MakeHudCorner(bLen, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 1.8, 1.8, 0), new CornerRadius(0, 3, 0, 0)));
                parts.HudCorners.Add(MakeHudCorner(bLen, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(1.8, 0, 0, 1.8), new CornerRadius(0, 0, 0, 3)));
                parts.HudCorners.Add(MakeHudCorner(bLen, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 1.8, 1.8), new CornerRadius(0, 0, 3, 0)));
                for (int i = 0; i < parts.HudCorners.Count; i++) layers.Children.Add(parts.HudCorners[i]);
            }
            else if (styleId == "DiamondCrystal")
            {
                outer.Background = Brushes.Transparent;
                double dSz = Math.Round(sz * 0.72);
                parts.DiamondBox = new Border
                {
                    Width = dSz,
                    Height = dSz,
                    CornerRadius = new CornerRadius(Math.Round(dSz * 0.24)),
                    BorderThickness = new Thickness(1.2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new RotateTransform(45),
                    IsHitTestVisible = false
                };
                layers.Children.Add(parts.DiamondBox);
            }
            else if (styleId == "TitaniumChrome")
            {
                outer.CornerRadius = new CornerRadius(Math.Round(sz * 0.30));
                outer.Padding = new Thickness(1.6);
                outer.Background = BuildChromeBrush(false);
                parts.InnerBox = new Border
                {
                    CornerRadius = new CornerRadius(Math.Round(sz * 0.27)),
                    Background = BuildDarkMirrorBrush(false),
                    IsHitTestVisible = false
                };
                layers.Children.Add(parts.InnerBox);
            }
            else if (styleId == "HexagonCrystal")
            {
                outer.Background = Brushes.Transparent;
                double c = sz / 2.0;
                double r = c - 1.5;
                PointCollection pts = new PointCollection();
                for (int k = 0; k < 6; k++)
                {
                    double ang = (k * 60.0 - 90.0) * Math.PI / 180.0;
                    pts.Add(new Point(c + r * Math.Cos(ang), c + r * Math.Sin(ang)));
                }
                parts.HexPoly = new Polygon
                {
                    Points = pts,
                    StrokeThickness = 1.25,
                    StrokeLineJoin = PenLineJoin.Round,
                    IsHitTestVisible = false
                };
                layers.Children.Add(parts.HexPoly);
            }
            else if (styleId == "Holographic")
            {
                outer.CornerRadius = new CornerRadius(Math.Round(sz * 0.30));
                outer.Padding = new Thickness(1.5);
                outer.Background = BuildHolographicBrush();
                parts.InnerBox = new Border
                {
                    CornerRadius = new CornerRadius(Math.Round(sz * 0.27)),
                    IsHitTestVisible = false
                };
                layers.Children.Add(parts.InnerBox);
            }
            else if (styleId == "NeumorphicInset")
            {
                outer.CornerRadius = new CornerRadius(Math.Round(sz * 0.30));
                outer.BorderThickness = new Thickness(1);
                outer.ClipToBounds = true;
            }
            else if (styleId == "DottedTech")
            {
                outer.CornerRadius = new CornerRadius(Math.Round(sz * 0.28));
                parts.DottedRect = new Rectangle
                {
                    RadiusX = Math.Round(sz * 0.28),
                    RadiusY = Math.Round(sz * 0.28),
                    StrokeThickness = 1.35,
                    StrokeDashArray = new DoubleCollection(new double[] { 3.2, 2.6 }),
                    IsHitTestVisible = false
                };
                layers.Children.Add(parts.DottedRect);
            }

            parts.ContentHost = new Grid();
            layers.Children.Add(parts.ContentHost);
            outer.Child = layers;
            outer.Tag = parts;

            ApplyVisualState(outer, p, false, false);
            return outer;
        }

        public static void SetContent(Border tile, UIElement content)
        {
            StyleParts parts = tile.Tag as StyleParts;
            if (parts != null && parts.ContentHost != null)
            {
                parts.ContentHost.Children.Clear();
                parts.ContentHost.Children.Add(content);
            }
            else
            {
                tile.Child = content;
            }
        }

        public static void ApplyVisualState(Border tile, ThemePalette p, bool hovered, bool isForeground)
        {
            StyleParts parts = tile.Tag as StyleParts;
            if (parts == null) return;
            string id = parts.StyleId;
            bool active = hovered || isForeground;

            if (id == "Circle" || id == "Squircle" || id == "SharpPrism" || id == "WideCapsule" || id == "OrganicLeaf")
            {
                if (id == "OrganicLeaf")
                {
                    CornerRadius leafCr = GetCornerRadius(id, parts.Size, tile.Height, hovered);
                    tile.CornerRadius = leafCr;
                    if (parts.InnerShade != null) parts.InnerShade.CornerRadius = leafCr;
                }

                tile.Background = ThemeManager.CreateOrbBodyBrush(p, active);
                tile.BorderBrush = isForeground && !hovered
                    ? ThemeManager.Brush(Color.FromArgb(205, p.Accent.R, p.Accent.G, p.Accent.B))
                    : ThemeManager.CreateOrbRimBrush(p, active);
            }
            else if (id == "DarkCrystal")
            {
                tile.Background = BuildDarkMirrorBrush(active);
                tile.BorderBrush = active
                    ? ThemeManager.Brush(Color.FromArgb(220, p.Accent.R, p.Accent.G, p.Accent.B))
                    : ThemeManager.Brush(Color.FromArgb(95, 255, 255, 255));
            }
            else if (id == "Borderless")
            {
                tile.Background = active ? ThemeManager.CreateOrbBodyBrush(p, hovered) : Brushes.Transparent;
                tile.BorderBrush = active ? ThemeManager.CreateOrbRimBrush(p, hovered) : Brushes.Transparent;
            }
            else if (id == "GlowPedestal")
            {
                LinearGradientBrush lg = new LinearGradientBrush();
                lg.StartPoint = new Point(0.5, 0);
                lg.EndPoint = new Point(0.5, 1);
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.35));
                Color glowC = active ? p.Accent : (p.IsLight ? Color.FromRgb(100, 116, 139) : Colors.White);
                lg.GradientStops.Add(new GradientStop(Color.FromArgb(active ? (byte)98 : (byte)46, glowC.R, glowC.G, glowC.B), 1.0));
                lg.Freeze();
                tile.Background = lg;
                tile.BorderBrush = ThemeManager.Brush(Color.FromArgb(active ? (byte)240 : (byte)140, glowC.R, glowC.G, glowC.B));
            }
            else if (id == "NeonRing")
            {
                tile.Background = ThemeManager.Brush(active ? Color.FromArgb(135, 16, 22, 40) : Color.FromArgb(105, 10, 14, 26));
                tile.BorderBrush = ThemeManager.Brush(active
                    ? Color.FromArgb(255, (byte)Math.Min(255, p.Accent.R + 40), (byte)Math.Min(255, p.Accent.G + 40), (byte)Math.Min(255, p.Accent.B + 40))
                    : Color.FromArgb(185, p.Accent.R, p.Accent.G, p.Accent.B));
            }
            else if (id == "DoubleGlass")
            {
                tile.Background = ThemeManager.Brush(Color.FromArgb(active ? (byte)55 : (byte)26, 255, 255, 255));
                tile.BorderBrush = ThemeManager.CreateOrbRimBrush(p, active);
                if (parts.InnerBox != null)
                {
                    parts.InnerBox.Background = ThemeManager.CreateOrbBodyBrush(p, active);
                    parts.InnerBox.BorderBrush = ThemeManager.Brush(Color.FromArgb(active ? (byte)155 : (byte)85, 255, 255, 255));
                }
            }
            else if (id == "AmbientAura")
            {
                if (parts.AuraEllipse != null)
                {
                    parts.AuraEllipse.Opacity = active ? 0.96 : 0.48;
                }
            }
            else if (id == "HudBrackets")
            {
                tile.Background = ThemeManager.Brush(active ? Color.FromArgb(88, p.Accent.R, p.Accent.G, p.Accent.B) : Color.FromArgb(62, 10, 14, 24));
                Brush brkCol = ThemeManager.Brush(active ? p.Accent : (p.IsLight ? Color.FromArgb(180, 15, 23, 42) : Color.FromArgb(185, 255, 255, 255)));
                if (parts.HudCorners != null)
                {
                    for (int i = 0; i < parts.HudCorners.Count; i++)
                        parts.HudCorners[i].BorderBrush = brkCol;
                }
            }
            else if (id == "DiamondCrystal" && parts.DiamondBox != null)
            {
                parts.DiamondBox.Background = ThemeManager.CreateOrbBodyBrush(p, active);
                parts.DiamondBox.BorderBrush = active
                    ? ThemeManager.Brush(Color.FromArgb(230, p.Accent.R, p.Accent.G, p.Accent.B))
                    : ThemeManager.CreateOrbRimBrush(p, false);
            }
            else if (id == "TitaniumChrome")
            {
                tile.Background = BuildChromeBrush(active);
                if (parts.InnerBox != null)
                    parts.InnerBox.Background = BuildDarkMirrorBrush(active);
            }
            else if (id == "HexagonCrystal" && parts.HexPoly != null)
            {
                parts.HexPoly.Fill = ThemeManager.CreateOrbBodyBrush(p, active);
                parts.HexPoly.Stroke = active
                    ? ThemeManager.Brush(Color.FromArgb(235, p.Accent.R, p.Accent.G, p.Accent.B))
                    : ThemeManager.CreateOrbRimBrush(p, false);
            }
            else if (id == "Holographic" && parts.InnerBox != null)
            {
                parts.InnerBox.Background = active
                    ? ThemeManager.Brush(Color.FromArgb(175, 24, 28, 46))
                    : ThemeManager.Brush(Color.FromArgb(160, 14, 18, 32));
            }
            else if (id == "NeumorphicInset")
            {
                if (active)
                {
                    tile.Background = ThemeManager.CreateOrbBodyBrush(p, true);
                    tile.BorderBrush = ThemeManager.CreateOrbRimBrush(p, true);
                }
                else
                {
                    LinearGradientBrush insetBg = new LinearGradientBrush(
                        Color.FromArgb(150, 4, 7, 16),
                        Color.FromArgb(95, 22, 28, 44),
                        90);
                    insetBg.Freeze();
                    tile.Background = insetBg;
                    LinearGradientBrush insetRim = new LinearGradientBrush(
                        Color.FromArgb(145, 0, 0, 0),
                        Color.FromArgb(115, 255, 255, 255),
                        90);
                    insetRim.Freeze();
                    tile.BorderBrush = insetRim;
                }
            }
            else if (id == "DottedTech" && parts.DottedRect != null)
            {
                tile.Background = active ? ThemeManager.CreateOrbBodyBrush(p, true) : ThemeManager.Brush(Color.FromArgb(14, 255, 255, 255));
                parts.DottedRect.Stroke = active
                    ? ThemeManager.Brush(Color.FromArgb(235, p.Accent.R, p.Accent.G, p.Accent.B))
                    : ThemeManager.Brush(p.IsLight ? Color.FromArgb(120, 15, 23, 42) : Color.FromArgb(135, 255, 255, 255));
                parts.DottedRect.StrokeDashArray = active ? null : new DoubleCollection(new double[] { 3.2, 2.6 });
            }
        }

        private static CornerRadius GetCornerRadius(string id, double sz, double h, bool hovered)
        {
            if (id == "Circle") return new CornerRadius(sz / 2.0);
            if (id == "WideCapsule") return new CornerRadius(h / 2.0);
            if (id == "SharpPrism") return new CornerRadius(6);
            if (id == "OrganicLeaf")
                return hovered ? new CornerRadius(6, 18, 6, 18) : new CornerRadius(18, 6, 18, 6);
            return new CornerRadius(Math.Round(sz * 0.30)); // Squircle (~13-14px)
        }

        private static Border MakeHudCorner(double len, HorizontalAlignment ha, VerticalAlignment va, Thickness bt, CornerRadius cr)
        {
            return new Border
            {
                Width = len,
                Height = len,
                HorizontalAlignment = ha,
                VerticalAlignment = va,
                Margin = new Thickness(2.5),
                BorderThickness = bt,
                CornerRadius = cr,
                IsHitTestVisible = false
            };
        }

        private static Brush BuildChromeBrush(bool bright)
        {
            LinearGradientBrush lg = new LinearGradientBrush();
            lg.StartPoint = new Point(0, 0);
            lg.EndPoint = new Point(1, 1);
            lg.GradientStops.Add(new GradientStop(Colors.White, 0.0));
            lg.GradientStops.Add(new GradientStop(Color.FromRgb(148, 163, 184), 0.32));
            lg.GradientStops.Add(new GradientStop(bright ? Color.FromRgb(120, 138, 165) : Color.FromRgb(51, 65, 85), 0.54));
            lg.GradientStops.Add(new GradientStop(Color.FromRgb(236, 242, 248), 0.80));
            lg.GradientStops.Add(new GradientStop(Color.FromRgb(100, 116, 139), 1.0));
            lg.Freeze();
            return lg;
        }

        private static Brush BuildDarkMirrorBrush(bool hovered)
        {
            LinearGradientBrush lg = new LinearGradientBrush(
                hovered ? Color.FromArgb(228, 36, 44, 66) : Color.FromArgb(208, 18, 22, 36),
                hovered ? Color.FromArgb(238, 16, 20, 34) : Color.FromArgb(228, 8, 11, 20),
                90);
            lg.Freeze();
            return lg;
        }

        private static Brush BuildHolographicBrush()
        {
            LinearGradientBrush lg = new LinearGradientBrush();
            lg.StartPoint = new Point(0, 0);
            lg.EndPoint = new Point(1, 1);
            lg.GradientStops.Add(new GradientStop(Color.FromRgb(56, 189, 248), 0.0));
            lg.GradientStops.Add(new GradientStop(Color.FromRgb(192, 132, 252), 0.35));
            lg.GradientStops.Add(new GradientStop(Color.FromRgb(244, 114, 182), 0.68));
            lg.GradientStops.Add(new GradientStop(Color.FromRgb(253, 224, 71), 1.0));
            lg.Freeze();
            return lg;
        }
    }
}
