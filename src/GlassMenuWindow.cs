using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Monolith
{
    public class GlassMenuItemDef
    {
        public string Label;
        public string Glyph;
        public string RightHint;
        public bool IsSeparator;
        public bool IsHeader;
        public bool IsChecked;
        public bool IsDanger;
        public Action OnClick;
    }

    /// <summary>
    /// Sleek frosted-glass context menu matching Monolith's modern Windows glass aesthetic
    /// instead of legacy white Win32 context menus.
    /// </summary>
    public class GlassMenuWindow : Window
    {
        public GlassMenuWindow(List<GlassMenuItemDef> items, ThemePalette palette, Window ownerDock, bool isTop)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;

            Border shell = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = ThemeManager.Brush(palette.ModalBg),
                BorderBrush = ThemeManager.Brush(palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                Margin = new Thickness(10),
                MinWidth = 216,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 24,
                    ShadowDepth = 6,
                    Opacity = 0.5,
                    Color = Colors.Black
                }
            };

            StackPanel stack = new StackPanel();
            for (int i = 0; i < items.Count; i++)
            {
                GlassMenuItemDef it = items[i];
                if (it.IsSeparator)
                {
                    stack.Children.Add(new Border
                    {
                        Height = 1,
                        Margin = new Thickness(8, 4, 8, 4),
                        Background = ThemeManager.Brush(palette.DividerColor)
                    });
                    continue;
                }

                if (it.IsHeader)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = it.Label,
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = ThemeManager.Brush(palette.TextMuted),
                        Margin = new Thickness(10, 5, 10, 4),
                        MaxWidth = 230,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                    continue;
                }

                Border row = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 7, 10, 7),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                DockPanel dp = new DockPanel();

                if (!string.IsNullOrEmpty(it.RightHint) || it.IsChecked)
                {
                    TextBlock hint = new TextBlock
                    {
                        Text = it.IsChecked ? "\u2713" : it.RightHint,
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = it.IsChecked ? 12 : 10.5,
                        FontWeight = it.IsChecked ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = ThemeManager.Brush(it.IsChecked ? palette.Accent : palette.TextMuted),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(12, 0, 0, 0)
                    };
                    DockPanel.SetDock(hint, Dock.Right);
                    dp.Children.Add(hint);
                }

                StackPanel left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                if (!string.IsNullOrEmpty(it.Glyph))
                {
                    bool isMdl2 = it.Glyph.Length == 1 && it.Glyph[0] >= 0xE700 && it.Glyph[0] <= 0xF8FF;
                    left.Children.Add(new TextBlock
                    {
                        Text = it.Glyph,
                        FontFamily = new FontFamily(isMdl2 ? "Segoe MDL2 Assets" : "Segoe UI Symbol"),
                        FontSize = 12.5,
                        Width = 18,
                        Foreground = ThemeManager.Brush(it.IsDanger ? Color.FromRgb(248, 113, 113) : palette.TextSecondary),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 7, 0)
                    });
                }

                TextBlock lbl = new TextBlock
                {
                    Text = it.Label,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12.5,
                    FontWeight = FontWeights.Medium,
                    Foreground = ThemeManager.Brush(it.IsDanger ? Color.FromRgb(248, 113, 113) : palette.TextPrimary),
                    VerticalAlignment = VerticalAlignment.Center
                };
                left.Children.Add(lbl);
                dp.Children.Add(left);
                row.Child = dp;

                GlassMenuItemDef captured = it;
                row.MouseEnter += delegate
                {
                    row.Background = ThemeManager.Brush(captured.IsDanger
                        ? Color.FromArgb(48, 239, 68, 68)
                        : Color.FromArgb(46, palette.Accent.R, palette.Accent.G, palette.Accent.B));
                };
                row.MouseLeave += delegate
                {
                    row.Background = Brushes.Transparent;
                };
                row.MouseLeftButtonUp += delegate
                {
                    Close();
                    if (captured.OnClick != null) captured.OnClick();
                };

                stack.Children.Add(row);
            }

            shell.Child = stack;
            Content = shell;

            Deactivated += delegate { try { Close(); } catch { } };
            PreviewKeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); };

            Loaded += delegate
            {
                NativeWin32.POINT pt;
                NativeWin32.GetCursorPos(out pt);
                PresentationSource src = PresentationSource.FromVisual(ownerDock);
                double dpiX = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformToDevice.M11 : 1.0;
                double dpiY = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformToDevice.M22 : 1.0;

                double cx = pt.X / dpiX;
                double cy = pt.Y / dpiY;
                double sw = SystemParameters.PrimaryScreenWidth;
                double sh = SystemParameters.PrimaryScreenHeight;

                double left = cx - ActualWidth / 2.0;
                if (left < 6) left = 6;
                if (left + ActualWidth > sw - 6) left = sw - ActualWidth - 6;

                double top = isTop ? (cy + 6) : (cy - ActualHeight - 4);
                if (top < 6) top = 6;
                if (top + ActualHeight > sh - 6) top = sh - ActualHeight - 6;

                Left = left;
                Top = top;
            };
        }
    }
}
