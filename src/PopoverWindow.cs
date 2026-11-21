using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Monolith
{
    /// <summary>
    /// Floating frosted-glass popover anchored directly above (or below) any clicked widget.
    /// Hosts rich interactive panels: Todos, Notes, Screen Time, Top Processes, Drives,
    /// Calendar, Volume Mixer, Weather Forecast, Pomodoro Modes, AI Limits, Battery, Commands.
    /// </summary>
    public class PopoverWindow : Window
    {
        private readonly MonolithConfig _cfg;
        private readonly ThemePalette _palette;
        private readonly SystemTelemetry _tel;
        private readonly Action _onChanged;

        public PopoverWindow(
            string widgetId,
            FrameworkElement anchor,
            Window ownerDock,
            MonolithConfig cfg,
            ThemePalette palette,
            SystemTelemetry tel,
            Action onChanged)
        {
            _cfg = cfg;
            _palette = palette;
            _tel = tel;
            _onChanged = onChanged;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;

            Border shell = new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = ThemeManager.Brush(palette.ModalBg),
                BorderBrush = ThemeManager.Brush(palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16),
                Margin = new Thickness(12),
                MinWidth = 280,
                MaxWidth = 360,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 26,
                    ShadowDepth = 6,
                    Opacity = 0.48,
                    Color = Colors.Black
                }
            };

            shell.Child = BuildContent(widgetId);
            Content = shell;

            Deactivated += delegate
            {
                try { Close(); } catch { }
            };
            PreviewKeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Escape) Close();
            };

            Loaded += delegate
            {
                PositionRelativeToAnchor(anchor, ownerDock);
            };
        }

        private void PositionRelativeToAnchor(FrameworkElement anchor, Window ownerDock)
        {
            try
            {
                Point pt = anchor.PointToScreen(new Point(anchor.ActualWidth / 2.0, 0));
                PresentationSource src = PresentationSource.FromVisual(ownerDock);
                double dpiX = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformToDevice.M11 : 1.0;
                double dpiY = src != null && src.CompositionTarget != null ? src.CompositionTarget.TransformToDevice.M22 : 1.0;

                double anchorCenterX = pt.X / dpiX;
                double left = anchorCenterX - ActualWidth / 2.0;
                double screenW = SystemParameters.PrimaryScreenWidth;
                double screenH = SystemParameters.PrimaryScreenHeight;

                if (left < 6) left = 6;
                if (left + ActualWidth > screenW - 6) left = screenW - ActualWidth - 6;

                double top;
                if (_cfg.IsTop)
                    top = ownerDock.Top + ownerDock.ActualHeight + 2;
                else
                    top = ownerDock.Top - ActualHeight + 4;

                if (top < 6) top = 6;
                if (top + ActualHeight > screenH - 6) top = screenH - ActualHeight - 6;

                Left = left;
                Top = top;
            }
            catch { }
        }

        private UIElement BuildContent(string id)
        {
            if (id == "todos") return BuildTodosPanel();
            if (id == "notes") return BuildNotesPanel();
            if (id == "screen_time") return BuildScreenTimePanel();
            if (id == "system_chart") return BuildSystemPanel();
            if (id == "storage") return BuildStoragePanel();
            if (id == "weather") return BuildWeatherPanel();
            if (id == "calendar") return BuildCalendarPanel();
            if (id == "volume") return BuildVolumePanel();
            if (id == "pomodoro") return BuildPomodoroPanel();
            if (id == "ai_limits") return BuildAiLimitsPanel();
            if (id == "battery") return BuildBatteryPanel();
            if (id == "commands") return BuildCommandsPanel();
            if (id == "clipboard") return BuildClipboardPanel();
            if (id == "workspaces") return BuildWorkspacesPanel();

            return MakeHeader("Monolith", "");
        }

        private UIElement MakeHeader(string title, string subtitle)
        {
            DockPanel dp = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            TextBlock t = new TextBlock
            {
                Text = title,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.TextPrimary)
            };
            DockPanel.SetDock(t, Dock.Left);
            dp.Children.Add(t);

            if (!string.IsNullOrEmpty(subtitle))
            {
                TextBlock sub = new TextBlock
                {
                    Text = subtitle,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 11.5,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(sub, Dock.Right);
                dp.Children.Add(sub);
            }
            return dp;
        }

        private Button MakeActionBtn(string label, bool primary, Action onClick)
        {
            Button b = new Button
            {
                Content = label,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = primary ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary),
                Background = primary ? ThemeManager.Brush(_palette.Accent) : ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand
            };
            b.Click += delegate { onClick(); };
            return b;
        }

        // ====== TODOS POPOVER ======
        private UIElement BuildTodosPanel()
        {
            StackPanel sp = new StackPanel { Width = 300 };
            sp.Children.Add(MakeHeader(L10n.T(_cfg, "Yapılacaklar", "Todos"), _cfg.Todos.Count + " " + L10n.T(_cfg, "görev", "items")));

            StackPanel listPanel = new StackPanel();
            ScrollViewer sv = new ScrollViewer { MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = listPanel };

            Action rebuildList = null;
            rebuildList = delegate
            {
                listPanel.Children.Clear();
                for (int i = 0; i < _cfg.Todos.Count; i++)
                {
                    TodoEntry item = _cfg.Todos[i];
                    Border row = new Border
                    {
                        CornerRadius = new CornerRadius(10),
                        Background = ThemeManager.Brush(_palette.ModalSurface),
                        Padding = new Thickness(10, 7, 8, 7),
                        Margin = new Thickness(0, 0, 0, 6)
                    };
                    DockPanel dp = new DockPanel();
                    CheckBox cb = new CheckBox
                    {
                        IsChecked = item.Done,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0)
                    };
                    cb.Checked += delegate { item.Done = true; ConfigStore.Save(_cfg); _onChanged(); rebuildList(); };
                    cb.Unchecked += delegate { item.Done = false; ConfigStore.Save(_cfg); _onChanged(); rebuildList(); };

                    TextBlock del = new TextBlock
                    {
                        Text = "\u2715",
                        FontSize = 11,
                        Foreground = ThemeManager.Brush(_palette.TextMuted),
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor = Cursors.Hand,
                        Margin = new Thickness(8, 0, 2, 0)
                    };
                    del.MouseLeftButtonUp += delegate
                    {
                        _cfg.Todos.Remove(item);
                        ConfigStore.Save(_cfg);
                        _onChanged();
                        rebuildList();
                    };

                    TextBlock txt = new TextBlock
                    {
                        Text = item.Text,
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = 12.5,
                        Foreground = ThemeManager.Brush(item.Done ? _palette.TextMuted : _palette.TextPrimary),
                        TextDecorations = item.Done ? TextDecorations.Strikethrough : null,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    DockPanel.SetDock(cb, Dock.Left);
                    DockPanel.SetDock(del, Dock.Right);
                    dp.Children.Add(cb);
                    dp.Children.Add(del);
                    dp.Children.Add(txt);
                    row.Child = dp;
                    listPanel.Children.Add(row);
                }
            };
            rebuildList();
            sp.Children.Add(sv);

            Grid addRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBox input = new TextBox
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12.5,
                Background = ThemeManager.Brush(_palette.ModalSurface),
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                CaretBrush = ThemeManager.Brush(_palette.TextPrimary),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                Padding = new Thickness(8, 6, 8, 6)
            };
            Action addTask = delegate
            {
                string val = (input.Text ?? "").Trim();
                if (string.IsNullOrEmpty(val)) return;
                _cfg.Todos.Insert(0, new TodoEntry { Id = Guid.NewGuid().ToString("N"), Text = val, Done = false });
                input.Text = "";
                ConfigStore.Save(_cfg);
                _onChanged();
                rebuildList();
            };
            input.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) addTask();
            };
            Grid.SetColumn(input, 0);
            addRow.Children.Add(input);

            Button addBtn = MakeActionBtn("+", true, addTask);
            addBtn.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(addBtn, 1);
            addRow.Children.Add(addBtn);

            sp.Children.Add(addRow);
            return sp;
        }

        // ====== NOTES POPOVER ======
        private UIElement BuildNotesPanel()
        {
            StackPanel sp = new StackPanel { Width = 310 };
            sp.Children.Add(MakeHeader(L10n.T(_cfg, "Hızlı Notlar", "Quick Notes"), L10n.T(_cfg, "Otomatik kayıt", "Auto-saved")));

            TextBox tb = new TextBox
            {
                Text = _cfg.NotesText ?? "",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 190,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                Background = ThemeManager.Brush(_palette.ModalSurface),
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                CaretBrush = ThemeManager.Brush(_palette.TextPrimary),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                Padding = new Thickness(10),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            tb.TextChanged += delegate
            {
                _cfg.NotesText = tb.Text;
                ConfigStore.Save(_cfg);
                _onChanged();
            };
            sp.Children.Add(tb);
            return sp;
        }

        // ====== SCREEN TIME POPOVER ======
        private UIElement BuildScreenTimePanel()
        {
            StackPanel sp = new StackPanel { Width = 310 };
            int total = _tel.GetTotalScreenTimeSeconds(_cfg);
            sp.Children.Add(MakeHeader(L10n.T(_cfg, "Bugünün Ekran Süresi", "Today's Screen Time"), SystemTelemetry.FormatDuration(total)));

            List<KeyValuePair<string, int>> sorted = new List<KeyValuePair<string, int>>();
            if (_cfg.ScreenTimeByApp != null)
            {
                foreach (KeyValuePair<string, int> kv in _cfg.ScreenTimeByApp)
                    sorted.Add(kv);
            }
            sorted.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b) { return b.Value.CompareTo(a.Value); });

            if (sorted.Count == 0)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, "Henuz veri toplanmadi. Uygulamalari kullandikca burada gorunecek.", "Tracking local foreground apps. Usage will appear here shortly."),
                    FontSize = 12,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    TextWrapping = TextWrapping.Wrap
                });
                return sp;
            }

            int maxSec = Math.Max(1, sorted[0].Value);
            for (int i = 0; i < Math.Min(7, sorted.Count); i++)
            {
                KeyValuePair<string, int> kv = sorted[i];
                StackPanel row = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
                DockPanel dp = new DockPanel();
                dp.Children.Add(new TextBlock
                {
                    Text = kv.Key,
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary)
                });
                TextBlock dur = new TextBlock
                {
                    Text = SystemTelemetry.FormatDuration(kv.Value),
                    FontSize = 12,
                    Foreground = ThemeManager.Brush(_palette.TextSecondary),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                DockPanel.SetDock(dur, Dock.Right);
                dp.Children.Add(dur);
                row.Children.Add(dp);

                Border track = new Border
                {
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    Margin = new Thickness(0, 4, 0, 0),
                    Background = ThemeManager.Brush(_palette.ModalSurface)
                };
                track.Child = new Border
                {
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = Math.Max(10, 300.0 * kv.Value / maxSec),
                    Background = ThemeManager.Brush(_palette.Accent)
                };
                row.Children.Add(track);
                sp.Children.Add(row);
            }
            return sp;
        }

        // ====== SYSTEM / TOP PROCESSES POPOVER ======
        private UIElement BuildSystemPanel()
        {
            if (_tel.TopMemoryProcs.Count == 0)
            {
                _tel.RefreshTopProcessesAsync();
            }
            StackPanel sp = new StackPanel { Width = 310 };
            sp.Children.Add(MakeHeader(
                L10n.T(_cfg, "Sistem Monitörü", "System Monitor"),
                string.Format("CPU {0}%  |  RAM {1:0.0}/{2:0.0} GB", _tel.CpuPercent, _tel.RamUsedGB, _tel.RamTotalGB)));

            sp.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, "En Çok Bellek Kullananlar", "Top Memory Consumers"),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(_palette.TextMuted),
                Margin = new Thickness(0, 0, 0, 8)
            });

            for (int i = 0; i < _tel.TopMemoryProcs.Count; i++)
            {
                ProcessStat ps = _tel.TopMemoryProcs[i];
                Border r = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Background = ThemeManager.Brush(_palette.ModalSurface),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                DockPanel dp = new DockPanel();
                dp.Children.Add(new TextBlock
                {
                    Text = ps.Name,
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary)
                });
                TextBlock mb = new TextBlock
                {
                    Text = string.Format("{0:0} MB", ps.MemoryMB),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ThemeManager.Brush(_palette.Accent),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                DockPanel.SetDock(mb, Dock.Right);
                dp.Children.Add(mb);
                r.Child = dp;
                sp.Children.Add(r);
            }

            Button tm = MakeActionBtn(L10n.T(_cfg, "Görev Yöneticisini Aç", "Open Task Manager"), true, delegate
            {
                try { Process.Start("taskmgr.exe"); } catch { }
                Close();
            });
            tm.Margin = new Thickness(0, 8, 0, 0);
            sp.Children.Add(tm);
            return sp;
        }

        // ====== STORAGE POPOVER ======
        private UIElement BuildStoragePanel()
        {
            StackPanel sp = new StackPanel { Width = 300 };
            sp.Children.Add(MakeHeader(L10n.T(_cfg, "Disk Sürücüleri", "Storage Drives"), _tel.Drives.Count + " " + L10n.T(_cfg, "disk", "drives")));

            for (int i = 0; i < _tel.Drives.Count; i++)
            {
                DriveStat d = _tel.Drives[i];
                Border box = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Background = ThemeManager.Brush(_palette.ModalSurface),
                    Padding = new Thickness(11, 9, 11, 9),
                    Margin = new Thickness(0, 0, 0, 8),
                    Cursor = Cursors.Hand
                };
                box.MouseLeftButtonUp += delegate
                {
                    try { Process.Start(new ProcessStartInfo(d.Name + "\\") { UseShellExecute = true }); } catch { }
                    Close();
                };

                StackPanel inner = new StackPanel();
                DockPanel dp = new DockPanel();
                dp.Children.Add(new TextBlock
                {
                    Text = d.Name + " (" + d.Label + ")",
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary)
                });
                TextBlock pct = new TextBlock
                {
                    Text = d.UsedPercent + "%",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = ThemeManager.Brush(d.UsedPercent >= 88 ? Color.FromRgb(239, 68, 68) : _palette.Accent),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                DockPanel.SetDock(pct, Dock.Right);
                dp.Children.Add(pct);
                inner.Children.Add(dp);

                inner.Children.Add(new TextBlock
                {
                    Text = string.Format(L10n.T(_cfg, "{0:0.0} GB bos / {1:0.0} GB toplam", "{0:0.0} GB free of {1:0.0} GB"), d.FreeGB, d.TotalGB),
                    FontSize = 11,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    Margin = new Thickness(0, 2, 0, 6)
                });

                Border track = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = ThemeManager.Brush(Color.FromArgb(40, 128, 128, 128)) };
                track.Child = new Border
                {
                    Height = 6,
                    CornerRadius = new CornerRadius(3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = Math.Max(8, 276.0 * d.UsedPercent / 100.0),
                    Background = ThemeManager.Brush(d.UsedPercent >= 88 ? Color.FromRgb(239, 68, 68) : _palette.Accent)
                };
                inner.Children.Add(track);
                box.Child = inner;
                sp.Children.Add(box);
            }
            return sp;
        }

        // ====== WEATHER FORECAST POPOVER ======
        private UIElement BuildWeatherPanel()
        {
            StackPanel sp = new StackPanel { Width = 290 };
            sp.Children.Add(MakeHeader(_tel.Weather.City, _tel.Weather.Condition));

            StackPanel hero = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            hero.Children.Add(new TextBlock
            {
                Text = _tel.Weather.IconGlyph,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                Foreground = ThemeManager.Brush(Color.FromRgb(251, 191, 36)),
                FontSize = 36,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            });
            StackPanel hm = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            hm.Children.Add(new TextBlock
            {
                Text = string.Format("{0:0}\u00B0C", _tel.Weather.TempC),
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.TextPrimary)
            });
            hm.Children.Add(new TextBlock
            {
                Text = string.Format("H: {0:0}\u00B0  L: {1:0}\u00B0  \u2022  {2:0} km/h", _tel.Weather.HighC, _tel.Weather.LowC, _tel.Weather.WindKmh),
                FontSize = 11.5,
                Foreground = ThemeManager.Brush(_palette.TextMuted)
            });
            hero.Children.Add(hm);
            sp.Children.Add(hero);

            if (_tel.Weather.Daily.Count > 0)
            {
                UniformGridCols(sp, _tel.Weather.Daily);
            }

            Button refBtn = MakeActionBtn(L10n.T(_cfg, "Yenile", "Refresh"), false, delegate
            {
                _tel.RefreshOnlineAsync(_cfg, true);
                Close();
            });
            refBtn.Margin = new Thickness(0, 10, 0, 0);
            sp.Children.Add(refBtn);
            return sp;
        }

        private void UniformGridCols(StackPanel parent, List<WeatherDayForecast> days)
        {
            Grid g = new Grid();
            for (int i = 0; i < days.Count; i++)
            {
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                WeatherDayForecast d = days[i];
                Border b = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Background = ThemeManager.Brush(_palette.ModalSurface),
                    Padding = new Thickness(6, 8, 6, 8),
                    Margin = new Thickness(i == 0 ? 0 : 4, 0, 0, 0)
                };
                StackPanel s = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                s.Children.Add(new TextBlock { Text = d.DayName, FontSize = 10.5, Foreground = ThemeManager.Brush(_palette.TextMuted), HorizontalAlignment = HorizontalAlignment.Center });
                s.Children.Add(new TextBlock
                {
                    Text = d.IconGlyph,
                    FontFamily = new FontFamily("Segoe UI Symbol"),
                    Foreground = ThemeManager.Brush(Color.FromRgb(125, 211, 252)),
                    FontSize = 18,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 3, 0, 3)
                });
                s.Children.Add(new TextBlock { Text = string.Format("{0:0}\u00B0 / {1:0}\u00B0", d.HighC, d.LowC), FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = ThemeManager.Brush(_palette.TextPrimary), HorizontalAlignment = HorizontalAlignment.Center });
                b.Child = s;
                Grid.SetColumn(b, i);
                g.Children.Add(b);
            }
            parent.Children.Add(g);
        }

        // ====== CALENDAR POPOVER ======
        private UIElement BuildCalendarPanel()
        {
            DateTime now = DateTime.Now;
            CultureInfo ci = L10n.IsTr(_cfg) ? new CultureInfo("tr-TR") : CultureInfo.InvariantCulture;
            StackPanel sp = new StackPanel { Width = 280 };
            sp.Children.Add(MakeHeader(now.ToString("MMMM yyyy", ci), now.ToString("dddd", ci)));

            Grid cal = new Grid();
            for (int c = 0; c < 7; c++) cal.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int r = 0; r < 7; r++) cal.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });

            string[] headers = L10n.IsTr(_cfg)
                ? new string[] { "Pt", "Sa", "Ça", "Pe", "Cu", "Ct", "Pz" }
                : new string[] { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" };
            for (int c = 0; c < 7; c++)
            {
                TextBlock h = new TextBlock
                {
                    Text = headers[c],
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(h, c);
                Grid.SetRow(h, 0);
                cal.Children.Add(h);
            }

            DateTime first = new DateTime(now.Year, now.Month, 1);
            int offset = ((int)first.DayOfWeek + 6) % 7; // Monday = 0
            int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);

            for (int day = 1; day <= daysInMonth; day++)
            {
                int slot = offset + day - 1;
                int row = 1 + slot / 7;
                int col = slot % 7;
                bool isToday = (day == now.Day);

                Border cell = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(14),
                    Background = isToday ? ThemeManager.Brush(_palette.Accent) : Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                cell.Child = new TextBlock
                {
                    Text = day.ToString(),
                    FontSize = 12,
                    FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = isToday ? Brushes.White : ThemeManager.Brush(_palette.TextPrimary),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(cell, col);
                Grid.SetRow(cell, row);
                cal.Children.Add(cell);
            }

            sp.Children.Add(cal);

            Border notifBtn = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = ThemeManager.Brush(_palette.ModalSurface),
                BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 7, 10, 7),
                Margin = new Thickness(0, 10, 0, 0),
                Cursor = Cursors.Hand
            };
            StackPanel nsp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            nsp.Children.Add(new TextBlock
            {
                Text = "\uE7E7",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 12.5,
                Foreground = ThemeManager.Brush(_palette.Accent),
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            nsp.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, "Windows Bildirimlerini Aç (Win+N)", "Open Windows Notifications (Win+N)"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(_palette.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            });
            notifBtn.Child = nsp;
            notifBtn.MouseEnter += delegate { notifBtn.BorderBrush = ThemeManager.Brush(_palette.Accent); };
            notifBtn.MouseLeave += delegate { notifBtn.BorderBrush = ThemeManager.Brush(_palette.ModalBorder); };
            notifBtn.MouseLeftButtonUp += delegate
            {
                Close();
                NativeWin32.OpenNotifications();
            };
            sp.Children.Add(notifBtn);

            return sp;
        }

        // ====== VOLUME POPOVER ======
        private UIElement BuildVolumePanel()
        {
            StackPanel sp = new StackPanel { Width = 280 };
            TextBlock pctLbl = new TextBlock
            {
                Text = _tel.VolumeMuted ? "MUTE" : (_tel.VolumePercent + "%"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.Accent)
            };

            DockPanel hdr = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
            hdr.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, "Ses Seviyesi", "Master Volume"),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(_palette.TextPrimary)
            });
            DockPanel.SetDock(pctLbl, Dock.Right);
            hdr.Children.Add(pctLbl);
            sp.Children.Add(hdr);

            Slider sl = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = _tel.VolumePercent,
                IsSnapToTickEnabled = true,
                TickFrequency = 1,
                Margin = new Thickness(0, 4, 0, 12)
            };
            sl.ValueChanged += delegate
            {
                int v = (int)Math.Round(sl.Value);
                NativeWin32.SetSystemVolume(v);
                _tel.VolumePercent = v;
                pctLbl.Text = v + "%";
                _onChanged();
            };
            sp.Children.Add(sl);

            StackPanel btns = new StackPanel { Orientation = Orientation.Horizontal };
            btns.Children.Add(MakeActionBtn(_tel.VolumeMuted ? L10n.T(_cfg, "Sesi Ac", "Unmute") : L10n.T(_cfg, "Sessize Al", "Mute"), false, delegate
            {
                NativeWin32.ToggleSystemMute();
                _tel.VolumeMuted = !_tel.VolumeMuted;
                _onChanged();
                Close();
            }));
            Button snd = MakeActionBtn(L10n.T(_cfg, "Ses Ayarlari", "Sound Settings"), true, delegate
            {
                try { Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }); } catch { }
                Close();
            });
            snd.Margin = new Thickness(8, 0, 0, 0);
            btns.Children.Add(snd);
            sp.Children.Add(btns);
            return sp;
        }

        // ====== POMODORO POPOVER ======
        private UIElement BuildPomodoroPanel()
        {
            StackPanel sp = new StackPanel { Width = 280 };
            sp.Children.Add(MakeHeader("Pomodoro", _tel.PomodoroPhase));

            StackPanel presets = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            presets.Children.Add(MakePresetBtn("Focus 25m", "Focus", 25));
            presets.Children.Add(MakePresetBtn("Break 5m", "Short", 5));
            presets.Children.Add(MakePresetBtn("Long 15m", "Long", 15));
            sp.Children.Add(presets);

            StackPanel ctrls = new StackPanel { Orientation = Orientation.Horizontal };
            ctrls.Children.Add(MakeActionBtn(_tel.PomodoroRunning ? L10n.T(_cfg, "Duraklat", "Pause") : L10n.T(_cfg, "Baslat", "Start"), true, delegate
            {
                _tel.PomodoroRunning = !_tel.PomodoroRunning;
                _onChanged();
                Close();
            }));
            Button rst = MakeActionBtn(L10n.T(_cfg, "Sifirla", "Reset"), false, delegate
            {
                _tel.PomodoroRunning = false;
                _tel.PomodoroSecondsLeft = _tel.PomodoroTotalSeconds;
                _onChanged();
                Close();
            });
            rst.Margin = new Thickness(8, 0, 0, 0);
            ctrls.Children.Add(rst);
            sp.Children.Add(ctrls);
            return sp;
        }

        private Button MakePresetBtn(string label, string phase, int minutes)
        {
            Button b = MakeActionBtn(label, _tel.PomodoroPhase == phase, delegate
            {
                _tel.PomodoroPhase = phase;
                _tel.PomodoroTotalSeconds = minutes * 60;
                _tel.PomodoroSecondsLeft = minutes * 60;
                _tel.PomodoroRunning = true;
                _onChanged();
                Close();
            });
            b.Margin = new Thickness(0, 0, 6, 0);
            b.Padding = new Thickness(9, 5, 9, 5);
            return b;
        }

        // ====== AI LIMITS POPOVER ======
        private UIElement BuildAiLimitsPanel()
        {
            StackPanel sp = new StackPanel { Width = 310 };
            sp.Children.Add(MakeHeader(L10n.T(_cfg, "AI Plan Limitleri", "AI Plan Usage"), L10n.T(_cfg, "Yerel takip", "Local tracker")));

            for (int i = 0; i < _cfg.AiQuotas.Count; i++)
            {
                AiQuotaItem q = _cfg.AiQuotas[i];
                Border box = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Background = ThemeManager.Brush(_palette.ModalSurface),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                StackPanel inner = new StackPanel();
                DockPanel dp = new DockPanel();
                dp.Children.Add(new TextBlock
                {
                    Text = q.Provider + " \u2022 " + q.Plan,
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary)
                });
                TextBlock pctLbl = new TextBlock
                {
                    Text = q.PercentUsed + "%  (" + q.ResetLabel + ")",
                    FontSize = 11.5,
                    Foreground = ThemeManager.Brush(_palette.TextSecondary),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                DockPanel.SetDock(pctLbl, Dock.Right);
                dp.Children.Add(pctLbl);
                inner.Children.Add(dp);

                Slider sl = new Slider { Minimum = 0, Maximum = 100, Value = q.PercentUsed, Margin = new Thickness(0, 5, 0, 0) };
                sl.ValueChanged += delegate
                {
                    q.PercentUsed = (int)Math.Round(sl.Value);
                    pctLbl.Text = q.PercentUsed + "%  (" + q.ResetLabel + ")";
                    ConfigStore.Save(_cfg);
                    _onChanged();
                };
                inner.Children.Add(sl);
                box.Child = inner;
                sp.Children.Add(box);
            }
            return sp;
        }

        // ====== BATTERY POPOVER ======
        private UIElement BuildBatteryPanel()
        {
            StackPanel sp = new StackPanel { Width = 260 };
            sp.Children.Add(MakeHeader(L10n.T(_cfg, "Güç & Pil Durumu", "Power & Battery"), _tel.BatterySub));
            sp.Children.Add(new TextBlock
            {
                Text = _tel.HasBattery
                    ? string.Format(L10n.T(_cfg, "Şarj seviyesi: %{0}\nDurum: {1}", "Charge level: {0}%\nStatus: {1}"), _tel.BatteryPercent, _tel.IsCharging ? "Şarj Oluyor" : "Pilde")
                    : L10n.T(_cfg, "Masaüstü / AC güç kaynağına bağlı.", "Connected to AC Desktop power."),
                FontSize = 12.5,
                Foreground = ThemeManager.Brush(_palette.TextSecondary),
                Margin = new Thickness(0, 0, 0, 12)
            });
            sp.Children.Add(MakeActionBtn(L10n.T(_cfg, "Güç Ayarlarını Aç", "Open Power Settings"), true, delegate
            {
                try { Process.Start(new ProcessStartInfo("ms-settings:powersleep") { UseShellExecute = true }); } catch { }
                Close();
            }));
            return sp;
        }

        // ====== QUICK COMMANDS POPOVER ======
        private UIElement BuildCommandsPanel()
        {
            StackPanel sp = new StackPanel { Width = 280 };
            sp.Children.Add(MakeHeader(L10n.T(_cfg, "Hızlı Komutlar", "Quick Commands"), "\u26A1"));

            for (int i = 0; i < _cfg.QuickCommands.Count; i++)
            {
                QuickCommandItem cmd = _cfg.QuickCommands[i];
                Border item = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Background = ThemeManager.Brush(_palette.ModalSurface),
                    Padding = new Thickness(11, 8, 11, 8),
                    Margin = new Thickness(0, 0, 0, 6),
                    Cursor = Cursors.Hand
                };
                item.MouseEnter += delegate { item.Background = ThemeManager.Brush(_palette.Accent); };
                item.MouseLeave += delegate { item.Background = ThemeManager.Brush(_palette.ModalSurface); };
                item.MouseLeftButtonUp += delegate
                {
                    try
                    {
                        ProcessStartInfo psi = new ProcessStartInfo(cmd.Command, cmd.Args ?? "");
                        psi.UseShellExecute = true;
                        Process.Start(psi);
                    }
                    catch { }
                    Close();
                };
                item.Child = new TextBlock
                {
                    Text = "\u25B8  " + cmd.Name,
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary)
                };
                sp.Children.Add(item);
            }
            return sp;
        }

        // ====== CLIPBOARD HISTORY POPOVER ======
        private UIElement BuildClipboardPanel()
        {
            StackPanel sp = new StackPanel { Width = 320 };
            sp.Children.Add(MakeHeader(L10n.T(_cfg, "Pano Geçmişi", "Clipboard History"), _tel.ClipboardHistory.Count + " / 15"));

            if (_tel.ClipboardHistory.Count == 0)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = L10n.T(_cfg, "Henüz kopyalanan metin yok. Ctrl+C ile kopyaladıkların burada listelenir.", "No copied text yet. Items you copy with Ctrl+C appear here."),
                    FontSize = 12,
                    Foreground = ThemeManager.Brush(_palette.TextMuted),
                    TextWrapping = TextWrapping.Wrap
                });
                return sp;
            }

            StackPanel items = new StackPanel();
            ScrollViewer sv = new ScrollViewer { MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = items };
            sv.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta * 0.5);
                e.Handled = true;
            };

            for (int i = 0; i < _tel.ClipboardHistory.Count; i++)
            {
                string val = _tel.ClipboardHistory[i];
                string preview = val.Replace("\r", " ").Replace("\n", " ");
                if (preview.Length > 95) preview = preview.Substring(0, 95) + "...";

                Border row = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Background = ThemeManager.Brush(_palette.ModalSurface),
                    BorderBrush = ThemeManager.Brush(_palette.ModalBorder),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 7, 10, 7),
                    Margin = new Thickness(0, 0, 0, 6),
                    Cursor = Cursors.Hand
                };
                row.Child = new TextBlock
                {
                    Text = preview,
                    FontSize = 11.8,
                    Foreground = ThemeManager.Brush(_palette.TextPrimary),
                    TextWrapping = TextWrapping.Wrap
                };
                row.MouseEnter += delegate { row.BorderBrush = ThemeManager.Brush(_palette.Accent); };
                row.MouseLeave += delegate { row.BorderBrush = ThemeManager.Brush(_palette.ModalBorder); };
                row.MouseLeftButtonUp += delegate
                {
                    try { Clipboard.SetText(val); } catch { }
                    Close();
                };
                items.Children.Add(row);
            }
            sp.Children.Add(sv);

            Button clr = MakeActionBtn(L10n.T(_cfg, "Geçmişi Temizle", "Clear History"), false, delegate
            {
                _tel.ClipboardHistory.Clear();
                _onChanged();
                Close();
            });
            clr.Margin = new Thickness(0, 8, 0, 0);
            sp.Children.Add(clr);
            return sp;
        }

        // ====== WORKSPACES & WINDOW TILING POPOVER ======
        private UIElement BuildWorkspacesPanel()
        {
            StackPanel sp = new StackPanel { Width = 310 };
            sp.Children.Add(MakeHeader(L10n.T(_cfg, "Çalışma Alanı & Pencere Diz", "Workspaces & Tiling"), L10n.T(_cfg, "Hızlı Düzen", "Quick Snap")));

            // Virtual desktop actions
            Grid vGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            for (int i = 0; i < 3; i++) vGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Button prev = MakeActionBtn("\u25C0 " + L10n.T(_cfg, "Önceki", "Prev"), false, delegate { Close(); NativeWin32.SwitchVirtualDesktop(false); });
            Button tv = MakeActionBtn(L10n.T(_cfg, "Tümü", "Overview"), true, delegate { Close(); NativeWin32.OpenTaskView(); });
            Button next = MakeActionBtn(L10n.T(_cfg, "Sonraki", "Next") + " \u25B6", false, delegate { Close(); NativeWin32.SwitchVirtualDesktop(true); });
            prev.Margin = new Thickness(0, 0, 4, 0);
            tv.Margin = new Thickness(2, 0, 2, 0);
            next.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetColumn(prev, 0);
            Grid.SetColumn(tv, 1);
            Grid.SetColumn(next, 2);
            vGrid.Children.Add(prev);
            vGrid.Children.Add(tv);
            vGrid.Children.Add(next);
            sp.Children.Add(vGrid);

            sp.Children.Add(new TextBlock
            {
                Text = L10n.T(_cfg, "Aktif Pencereyi Hizala (Tiling)", "Snap Active Window"),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(_palette.TextMuted),
                Margin = new Thickness(0, 2, 0, 8)
            });

            IntPtr targetWin = _tel.LastActiveExternalWindow;
            Grid snapGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            snapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            snapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            snapGrid.RowDefinitions.Add(new RowDefinition());
            snapGrid.RowDefinitions.Add(new RowDefinition());

            Button sLeft = MakeActionBtn("\u25E7 " + L10n.T(_cfg, "Sol Yarım", "Left Half"), false, delegate { Close(); NativeWin32.SnapWindow(targetWin, "left"); });
            Button sRight = MakeActionBtn("\u25E8 " + L10n.T(_cfg, "Sağ Yarım", "Right Half"), false, delegate { Close(); NativeWin32.SnapWindow(targetWin, "right"); });
            Button sCenter = MakeActionBtn("\u25A3 " + L10n.T(_cfg, "Ortala (72%)", "Center"), false, delegate { Close(); NativeWin32.SnapWindow(targetWin, "center"); });
            Button sMax = MakeActionBtn("\u25A2 " + L10n.T(_cfg, "Tam Ekran", "Maximize"), false, delegate { Close(); NativeWin32.SnapWindow(targetWin, "max"); });
            sLeft.Margin = new Thickness(0, 0, 4, 6);
            sRight.Margin = new Thickness(4, 0, 0, 6);
            sCenter.Margin = new Thickness(0, 0, 4, 0);
            sMax.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetRow(sLeft, 0); Grid.SetColumn(sLeft, 0);
            Grid.SetRow(sRight, 0); Grid.SetColumn(sRight, 1);
            Grid.SetRow(sCenter, 1); Grid.SetColumn(sCenter, 0);
            Grid.SetRow(sMax, 1); Grid.SetColumn(sMax, 1);
            snapGrid.Children.Add(sLeft);
            snapGrid.Children.Add(sRight);
            snapGrid.Children.Add(sCenter);
            snapGrid.Children.Add(sMax);
            sp.Children.Add(snapGrid);

            Grid extras = new Grid();
            extras.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            extras.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Button newDesk = MakeActionBtn("+ " + L10n.T(_cfg, "Yeni Masaüstü", "New Desktop"), false, delegate { Close(); NativeWin32.CreateVirtualDesktop(); });
            Button showDesk = MakeActionBtn(L10n.T(_cfg, "Masaüstünü Göster", "Show Desktop"), false, delegate { Close(); NativeWin32.ShowDesktop(); });
            newDesk.Margin = new Thickness(0, 0, 4, 0);
            showDesk.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetColumn(newDesk, 0);
            Grid.SetColumn(showDesk, 1);
            extras.Children.Add(newDesk);
            extras.Children.Add(showDesk);
            sp.Children.Add(extras);

            return sp;
        }
    }
}
