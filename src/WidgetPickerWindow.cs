using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Monolith
{
    /// <summary>
    /// Monolith-style "All Widgets" floating frosted-glass gallery modal with search,
    /// category sidebar, live visual mini-previews, active count badge, and instant dock sync.
    /// </summary>
    public class WidgetPickerWindow : Window
    {
        private readonly MonolithConfig _cfg;
        private readonly ThemePalette _palette;
        private readonly SystemTelemetry _tel;
        private readonly Action _onDockChanged;

        private string _selectedCategory = "All";
        private string _searchQuery = "";
        private WrapPanel _galleryPanel;
        private StackPanel _catSidebar;
        private TextBlock _activeCountBadge;

        public WidgetPickerWindow(MonolithConfig cfg, ThemePalette palette, SystemTelemetry tel, Action onDockChanged)
        {
            _cfg = cfg;
            _palette = palette;
            _tel = tel;
            _onDockChanged = onDockChanged;

            Title = "Monolith Widgets";
            Width = 890;
            Height = 580;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            PreviewKeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) Close();
            };

            BuildUI();
        }

        private void BuildUI()
        {
            Border shell = new Border
            {
                CornerRadius = new CornerRadius(20),
                Background = ThemeManager.Brush(_palette.ModalBg),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(16),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 32,
                    ShadowDepth = 10,
                    Opacity = 0.55,
                    Color = Colors.Black
                }
            };

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // ====== HEADER ======
            Border header = new Border
            {
                BorderBrush = ThemeManager.Brush(_palette.DividerColor),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(20, 0, 16, 0),
                Background = Brushes.Transparent
            };

            DockPanel hdrDock = new DockPanel { VerticalAlignment = VerticalAlignment.Center };

            StackPanel titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleRow.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, "Tüm Widget'lar", "All Widgets"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            });

            Border totalPill = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            totalPill.Child = new TextBlock
            {
                Text = WidgetCatalog.All.Count.ToString(),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.TextSecondary)
            };
            titleRow.Children.Add(totalPill);

            Border activePill = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = ThemeManager.Brush(Color.FromArgb(40, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B)),
                BorderBrush = ThemeManager.Brush(Color.FromArgb(120, _palette.Accent.R, _palette.Accent.G, _palette.Accent.B)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 2, 9, 2),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _activeCountBadge = new TextBlock
            {
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(_palette.Accent)
            };
            activePill.Child = _activeCountBadge;
            titleRow.Children.Add(activePill);

            titleRow.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, "  \u2022  Dock üzerinde sürükleyip sıralayabilirsin", "  \u2022  Drag widgets on the dock to reorder"),
                FontSize = 11.5,
                Foreground = ThemeManager.Brush(_palette.TextMuted),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            });

            DockPanel.SetDock(titleRow, Dock.Left);
            hdrDock.Children.Add(titleRow);

            TextBlock closeTxt = new TextBlock
            {
                Text = "\u2715",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(_palette.TextSecondary),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Border closeBtn = new Border
            {
                Width = 30,
                Height = 30,
                CornerRadius = new CornerRadius(15),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Child = closeTxt
            };
            closeBtn.MouseEnter += delegate
            {
                closeBtn.Background = ThemeManager.Brush(Color.FromRgb(239, 68, 68));
                closeTxt.Foreground = Brushes.White;
            };
            closeBtn.MouseLeave += delegate
            {
                closeBtn.Background = ThemeManager.Brush(_palette.ModalSurface);
                closeTxt.Foreground = ThemeManager.Brush(_palette.TextSecondary);
            };
            closeBtn.PreviewMouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                Close();
            };
            DockPanel.SetDock(closeBtn, Dock.Right);
            hdrDock.Children.Add(closeBtn);

            header.Child = hdrDock;
            header.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                if (e.ButtonState == MouseButtonState.Pressed && !closeBtn.IsMouseOver)
                {
                    try { DragMove(); } catch { }
                }
            };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            // ====== BODY: SIDEBAR + GALLERY ======
            Grid body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(206) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border leftBorder = new Border
            {
                BorderBrush = ThemeManager.Brush(_palette.DividerColor),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(14, 14, 14, 14)
            };
            StackPanel leftStack = new StackPanel();

            // Search input
            Border searchBox = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(9, 5, 9, 5),
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid sg = new Grid();
            TextBox searchTb = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.5,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                CaretBrush = ThemeManager.Brush(_palette.TextPrimary),
                Margin = new Thickness(20, 0, 0, 0)
            };
            TextBlock searchIcon = new TextBlock
            {
                Text = "\uE721",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Foreground = ThemeManager.Brush(_palette.TextMuted),
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            TextBlock ph = new TextBlock
            {
                Text = L10n.T(_cfg, "Widget ara...", "Search widgets..."),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Foreground = ThemeManager.Brush(_palette.TextMuted),
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20, 0, 0, 0)
            };
            searchTb.TextChanged += delegate
            {
                _searchQuery = (searchTb.Text ?? "").Trim();
                ph.Visibility = string.IsNullOrEmpty(_searchQuery) ? Visibility.Visible : Visibility.Collapsed;
                RefreshGallery();
            };
            sg.Children.Add(searchIcon);
            sg.Children.Add(ph);
            sg.Children.Add(searchTb);
            searchBox.Child = sg;
            leftStack.Children.Add(searchBox);

            _catSidebar = new StackPanel();
            leftStack.Children.Add(_catSidebar);
            leftBorder.Child = leftStack;
            Grid.SetColumn(leftBorder, 0);
            body.Children.Add(leftBorder);

            // Right gallery pane
            _galleryPanel = new WrapPanel { Margin = new Thickness(16, 14, 6, 14) };
            ScrollViewer sv = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _galleryPanel
            };
            sv.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta * 0.5);
                e.Handled = true;
            };
            Grid.SetColumn(sv, 1);
            body.Children.Add(sv);

            Grid.SetRow(body, 1);
            root.Children.Add(body);

            shell.Child = root;
            Content = shell;

            RefreshSidebar();
            RefreshGallery();
        }

        private void RefreshSidebar()
        {
            _catSidebar.Children.Clear();
            for (int i = 0; i < WidgetCatalog.Categories.Length; i++)
            {
                string cat = WidgetCatalog.Categories[i];
                bool selected = (cat == _selectedCategory);

                int count = 0;
                for (int j = 0; j < WidgetCatalog.All.Count; j++)
                    if (cat == "All" || WidgetCatalog.All[j].Category == cat) count++;

                Border item = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(11, 8, 11, 8),
                    Margin = new Thickness(0, 0, 0, 4),
                    Cursor = Cursors.Hand,
                    Background = selected ? ThemeManager.Brush(_palette.Accent) : Brushes.Transparent
                };
                DockPanel dp = new DockPanel();
                dp.Children.Add(new TextBlock
                {
                    Text = WidgetCatalog.GetCategoryLabel(_cfg, cat),
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 12.5,
                    FontWeight = selected ? FontWeights.Bold : FontWeights.Medium,
                    Foreground = selected ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary)
                });
                TextBlock cTxt = new TextBlock
                {
                    Text = count.ToString(),
                    FontSize = 11,
                    Foreground = selected ? ThemeManager.Brush(Color.FromArgb(215, 255, 255, 255)) : ThemeManager.Brush(_palette.TextMuted),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(cTxt, Dock.Right);
                dp.Children.Add(cTxt);
                item.Child = dp;

                if (!selected)
                {
                    item.MouseEnter += delegate { item.Background = ThemeManager.Brush(_palette.ModalSurface); };
                    item.MouseLeave += delegate { item.Background = Brushes.Transparent; };
                }
                item.MouseLeftButtonUp += delegate
                {
                    _selectedCategory = cat;
                    RefreshSidebar();
                    RefreshGallery();
                };
                _catSidebar.Children.Add(item);
            }
        }

        public void RefreshGallery()
        {
            _activeCountBadge.Text = _cfg.ActiveWidgets.Count + " " + L10n.T(_cfg, "aktif", "in bar");
            _galleryPanel.Children.Clear();

            DockMetrics previewMetrics = ThemeManager.GetMetrics("S");
            string q = _searchQuery.ToLowerInvariant();
            for (int i = 0; i < WidgetCatalog.All.Count; i++)
            {
                WidgetDef def = WidgetCatalog.All[i];
                if (_selectedCategory != "All" && def.Category != _selectedCategory) continue;
                if (!string.IsNullOrEmpty(q))
                {
                    string hay = (def.NameTr + " " + def.NameEn + " " + def.DescTr + " " + def.DescEn + " " + def.Category).ToLowerInvariant();
                    if (hay.IndexOf(q, StringComparison.Ordinal) < 0) continue;
                }

                bool active = _cfg.ActiveWidgets.Contains(def.Id);
                _galleryPanel.Children.Add(BuildGalleryCard(def, active, previewMetrics));
            }
        }

        private Border BuildGalleryCard(WidgetDef def, bool active, DockMetrics previewMetrics)
        {
            Border card = new Border
            {
                Width = 294,
                CornerRadius = new CornerRadius(15),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(active ? _palette.Accent : _palette.ModalBorder),
                BorderThickness = new Thickness(active ? 1.6 : 1.0),
                Padding = new Thickness(12, 11, 12, 11),
                Margin = new Thickness(0, 0, 12, 12),
                Cursor = Cursors.Hand
            };

            StackPanel outer = new StackPanel();

            // Live miniature widget preview box (just like monolith.app!)
            Border previewStage = new Border
            {
                Height = 58,
                CornerRadius = new CornerRadius(11),
                Background = ThemeManager.Brush(_palette.IsLight ? Color.FromRgb(230, 234, 242) : Color.FromRgb(14, 16, 22)),
                BorderBrush = ThemeManager.Brush(_palette.DividerColor),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 9),
                ClipToBounds = true
            };
            WidgetCardHandle livePreview = WidgetFactory.CreateCard(
                def.Id, _cfg, _palette, previewMetrics, _tel, false,
                delegate { }, delegate { }, delegate { }, delegate { });
            if (livePreview != null)
            {
                livePreview.Container.IsHitTestVisible = false;
                livePreview.Container.HorizontalAlignment = HorizontalAlignment.Center;
                livePreview.Container.VerticalAlignment = VerticalAlignment.Center;
                previewStage.Child = livePreview.Container;
            }
            outer.Children.Add(previewStage);

            // Footer row: title + description + add/added badge
            Grid footer = new Grid();
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, def.NameTr, def.NameEn),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.8,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            info.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, def.DescTr, def.DescEn),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 10.5,
                Foreground = ThemeManager.Brush(_palette.TextMuted),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 6, 0)
            });
            Grid.SetColumn(info, 0);
            footer.Children.Add(info);

            Border statusPill = new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 4, 10, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = active
                    ? ThemeManager.Brush(_palette.Accent)
                    : ThemeManager.Brush(Color.FromArgb(40, 148, 163, 184))
            };
            statusPill.Child = new TextBlock
            {
                Text = active ? L10n.T(_cfg, "\u2713 Eklendi", "\u2713 Added") : L10n.T(_cfg, "+ Ekle", "+ Add"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = active ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary)
            };
            Grid.SetColumn(statusPill, 1);
            footer.Children.Add(statusPill);

            outer.Children.Add(footer);
            card.Child = outer;

            card.MouseLeftButtonUp += delegate
            {
                if (_cfg.ActiveWidgets.Contains(def.Id))
                    _cfg.ActiveWidgets.Remove(def.Id);
                else
                    _cfg.ActiveWidgets.Add(def.Id);

                ConfigStore.Save(_cfg);
                RefreshGallery();
                if (_onDockChanged != null) _onDockChanged();
            };

            return card;
        }
    }
}
