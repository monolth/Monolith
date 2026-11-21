using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Monolith
{
    public class WidgetDef
    {
        public string Id;
        public string Category; // Clocks, System, Media, Productivity, Weather, Finance, AI & Dev
        public string NameTr;
        public string NameEn;
        public string DescTr;
        public string DescEn;
        public string IconGlyph;
        public string BadgeColorHex;
        public double BaseWidth;
    }

    public class WidgetCardHandle
    {
        public string Id;
        public Border Container;
        public Border RemoveBadge;
        public Action RefreshAction;
    }

    public static class WidgetCatalog
    {
        public static readonly string[] Categories = new string[]
        {
            "All",
            "Clocks",
            "System",
            "Media",
            "Productivity",
            "Weather",
            "Finance",
            "AI & Dev"
        };

        public static string GetCategoryLabel(MonolithConfig cfg, string cat)
        {
            if (cat == "All") return L10n.T(cfg, "Tümü", "All");
            if (cat == "Clocks") return L10n.T(cfg, "Saat & Tarih", "Clocks");
            if (cat == "System") return L10n.T(cfg, "Sistem", "System");
            if (cat == "Media") return L10n.T(cfg, "Medya & Ses", "Media & Audio");
            if (cat == "Productivity") return L10n.T(cfg, "Verimlilik", "Productivity");
            if (cat == "Weather") return L10n.T(cfg, "Hava Durumu", "Weather");
            if (cat == "Finance") return L10n.T(cfg, "Finans", "Finance");
            if (cat == "AI & Dev") return L10n.T(cfg, "AI & Geliştirici", "AI & Developer");
            return cat;
        }

        public static readonly List<WidgetDef> All = new List<WidgetDef>
        {
            // AI & Dev
            new WidgetDef { Id="ai_limits", Category="AI & Dev", NameTr="AI Plan Limitleri", NameEn="AI Plan Usage", DescTr="Claude, ChatGPT ve Gemini oturum limit çubukları", DescEn="Live usage bars & reset timers for Claude, ChatGPT, Gemini", IconGlyph="\u2728", BadgeColorHex="#10A37F", BaseWidth=174 },
            new WidgetDef { Id="github", Category="AI & Dev", NameTr="GitHub Aktivite", NameEn="GitHub Activity", DescTr="7x4 katkı ısı haritası ve profil özeti", DescEn="7x4 contribution heatmap & profile summary", IconGlyph="\uE943", BadgeColorHex="#22C55E", BaseWidth=138 },
            new WidgetDef { Id="commands", Category="AI & Dev", NameTr="Hızlı Komutlar", NameEn="Quick Commands", DescTr="Tek tıkla sistem ve geliştirici kısayolları", DescEn="One-click developer & system launcher palette", IconGlyph="\u26A1", BadgeColorHex="#A855F7", BaseWidth=68 },

            // Media
            new WidgetDef { Id="now_playing", Category="Media", NameTr="Şimdi Çalıyor", NameEn="Now Playing", DescTr="Spotify/YouTube başlığı ve medya kontrol düğmeleri", DescEn="Live track title with previous/play/next buttons", IconGlyph="\u266B", BadgeColorHex="#EC4899", BaseWidth=206 },
            new WidgetDef { Id="volume", Category="Media", NameTr="Ses Kontrolü", NameEn="Volume Control", DescTr="Tekerlekle anlık ses ayarı ve sessize alma", DescEn="Scroll wheel master volume slider & mute", IconGlyph="\uE767", BadgeColorHex="#3B82F6", BaseWidth=86 },

            // System
            new WidgetDef { Id="system_chart", Category="System", NameTr="CPU & RAM Grafiği", NameEn="CPU & RAM Chart", DescTr="Canlı sparkline grafiği ve en çok kullanan uygulamalar", DescEn="Live CPU/RAM sparkline with top processes popover", IconGlyph="\uE9D9", BadgeColorHex="#06B6D4", BaseWidth=146 },
            new WidgetDef { Id="cpu_ring", Category="System", NameTr="CPU Halkası", NameEn="CPU Gauge", DescTr="Kompakt dairesel CPU yükü göstergesi", DescEn="Compact circular CPU usage gauge", IconGlyph="\uE950", BadgeColorHex="#3B82F6", BaseWidth=58 },
            new WidgetDef { Id="ram_ring", Category="System", NameTr="RAM Halkası", NameEn="RAM Gauge", DescTr="Kompakt dairesel bellek kullanımı", DescEn="Compact circular memory gauge", IconGlyph="\uE964", BadgeColorHex="#8B5CF6", BaseWidth=58 },
            new WidgetDef { Id="storage", Category="System", NameTr="Depolama", NameEn="Storage", DescTr="Boş disk alanı, yüzde ve doluluk çubuğu", DescEn="Free disk space & visual usage progress bar", IconGlyph="\uEDA2", BadgeColorHex="#F97316", BaseWidth=140 },
            new WidgetDef { Id="network", Category="System", NameTr="Ağ Hızı", NameEn="Network", DescTr="Canlı indirme/yükleme hızı ve akış göstergesi", DescEn="Real-time download & upload speed monitor", IconGlyph="\uE839", BadgeColorHex="#14B8A6", BaseWidth=118 },
            new WidgetDef { Id="battery", Category="System", NameTr="Pil Kartı", NameEn="Battery Pill", DescTr="Canlı Monolith pil hap göstergesi", DescEn="Vivid Monolith signature battery percentage pill", IconGlyph="\uE83F", BadgeColorHex="#2563EB", BaseWidth=68 },
            new WidgetDef { Id="quick_toggles", Category="System", NameTr="Hızlı Anahtarlar", NameEn="Quick Toggles", DescTr="Ses, Wi-Fi, Bluetooth ve Kilitle kısayolları", DescEn="Mute, Wi-Fi, Bluetooth and Lock shortcuts", IconGlyph="\uE713", BadgeColorHex="#6366F1", BaseWidth=120 },
            new WidgetDef { Id="recycle_bin", Category="System", NameTr="Geri Dönüşüm", NameEn="Recycle Bin", DescTr="Çöp kutusu öğe sayısı ve tek tıkla temizleme", DescEn="Live Recycle Bin item count & quick empty", IconGlyph="\uE74D", BadgeColorHex="#64748B", BaseWidth=76 },

            // Productivity
            new WidgetDef { Id="pomodoro", Category="Productivity", NameTr="Pomodoro", NameEn="Pomodoro", DescTr="Odak sayacı, halka göstergesi ve mola modları", DescEn="Focus countdown ring with pause & break modes", IconGlyph="\uE916", BadgeColorHex="#EF4444", BaseWidth=118 },
            new WidgetDef { Id="screen_time", Category="Productivity", NameTr="Ekran Süresi", NameEn="Screen Time", DescTr="Saatlik aktivite çubuk grafiği ve uygulama süresi", DescEn="Local hourly activity chart & per-app breakdown", IconGlyph="\uE823", BadgeColorHex="#3B82F6", BaseWidth=132 },
            new WidgetDef { Id="todos", Category="Productivity", NameTr="Yapılacaklar", NameEn="Todos", DescTr="İlerleme halkası ve hızlı görev listesi", DescEn="Completion ring & interactive checklist popover", IconGlyph="\uE73A", BadgeColorHex="#22C55E", BaseWidth=142 },
            new WidgetDef { Id="notes", Category="Productivity", NameTr="Hızlı Notlar", NameEn="Quick Notes", DescTr="Anında erişilebilen yapışkan not penceresi", DescEn="Glanceable sticky notes saved locally", IconGlyph="\uE70B", BadgeColorHex="#EAB308", BaseWidth=116 },
            new WidgetDef { Id="clipboard", Category="Productivity", NameTr="Pano Geçmişi", NameEn="Clipboard History", DescTr="Son 15 kopyalanan metne tek tıkla erişim", DescEn="One-click access to your last 15 copied texts", IconGlyph="\uE77F", BadgeColorHex="#06B6D4", BaseWidth=124 },
            new WidgetDef { Id="workspaces", Category="Productivity", NameTr="Çalışma Alanı & Diz", NameEn="Workspaces & Snap", DescTr="Sanal masaüstü geçişi, Görev Görünümü ve pencere hizalama", DescEn="Virtual desktops, Task View & window tiling", IconGlyph="\uE7C4", BadgeColorHex="#6366F1", BaseWidth=112 },
            new WidgetDef { Id="control_center", Category="System", NameTr="Kontrol Merkezi", NameEn="Control Center", DescTr="Windows 11 tarzı şekillendirilebilir Wi-Fi, Bluetooth, parlaklık, ses ve güç paneli", DescEn="Customizable Windows 11-style connectivity, brightness, volume & power hub", IconGlyph="\uE9E9", BadgeColorHex="#3B82F6", BaseWidth=82 },
            new WidgetDef { Id="spotlight", Category="Productivity", NameTr="Hızlı Arama", NameEn="Quick Search", DescTr="Windows 11 tarzı uygulama, dosya, ayar arama ve hesap makinesi", DescEn="Windows 11-style app, file, settings search & live calculator", IconGlyph="\uE721", BadgeColorHex="#A855F7", BaseWidth=104 },
            new WidgetDef { Id="stopwatch", Category="Productivity", NameTr="Kronometre", NameEn="Stopwatch", DescTr="Satır içi başlat/duraklat/sıfırla kronometre", DescEn="Inline start/pause/reset stopwatch", IconGlyph="\uE916", BadgeColorHex="#0EA5E9", BaseWidth=114 },
            new WidgetDef { Id="day_timeline", Category="Productivity", NameTr="Gün Zaman Çizgisi", NameEn="Day Timeline", DescTr="Günün ilerleyişini gösteren renkli zaman çubuğu", DescEn="Color-coded progress bar across your day", IconGlyph="\uE787", BadgeColorHex="#8B5CF6", BaseWidth=130 },

            // Weather
            new WidgetDef { Id="weather", Category="Weather", NameTr="Hava Durumu", NameEn="Weather", DescTr="Canlı sıcaklık, durum ikonu ve 4 günlük tahmin", DescEn="Live temperature, condition & 4-day forecast", IconGlyph="\u26C5", BadgeColorHex="#38BDF8", BaseWidth=104 },

            // Finance
            new WidgetDef { Id="crypto", Category="Finance", NameTr="Kripto & Döviz", NameEn="Crypto & FX", DescTr="Canlı BTC, ETH veya USD/TRY fiyat kartı", DescEn="Live BTC, ETH or USD/TRY price & 24h delta", IconGlyph="\u20BF", BadgeColorHex="#F59E0B", BaseWidth=124 },

            // Clocks
            new WidgetDef { Id="today", Category="Clocks", NameTr="Bugün (Saat/Tarih)", NameEn="Today (Clock)", DescTr="Saat, tarih ve tıklayınca açılan takvim", DescEn="Time, date & clickable monthly calendar popover", IconGlyph="\uE787", BadgeColorHex="#3B82F6", BaseWidth=98 },
            new WidgetDef { Id="flip_clock", Category="Clocks", NameTr="Flip Saat", NameEn="Flip Clock", DescTr="Klasik bölünmüş kart tasarımlı dijital saat", DescEn="Split-flap tactile digital clock cards", IconGlyph="\uE823", BadgeColorHex="#64748B", BaseWidth=90 },
            new WidgetDef { Id="analog_clock", Category="Clocks", NameTr="Analog Saat", NameEn="Analog Clock", DescTr="Canlı saniye ibreli minimalist analog saat", DescEn="Minimal vector analog clock with live second hand", IconGlyph="\uE916", BadgeColorHex="#EC4899", BaseWidth=54 },
            new WidgetDef { Id="world_clocks", Category="Clocks", NameTr="Dünya Saatleri", NameEn="World Clocks", DescTr="Birden fazla şehir için canlı saatler", DescEn="Side-by-side live times across timezones", IconGlyph="\uE774", BadgeColorHex="#14B8A6", BaseWidth=142 }
        };

        public static WidgetDef Find(string id)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].Id == id) return All[i];
            return null;
        }
    }

    public static class WidgetFactory
    {
        public static WidgetCardHandle CreateCard(
            string id,
            MonolithConfig cfg,
            ThemePalette palette,
            DockMetrics m,
            SystemTelemetry tel,
            bool editMode,
            Action<string, FrameworkElement> onOpenPopover,
            Action<string> onRemoveWidget,
            Action<string> onHoverEnter,
            Action onHoverLeave)
        {
            WidgetDef def = WidgetCatalog.Find(id);
            if (def == null) return null;

            double scale = m.BarHeight / 64.0;
            double cardW = Math.Round(def.BaseWidth * scale);
            double cardH = m.CardHeight;

            Grid outer = new Grid();
            outer.Margin = new Thickness(m.ItemGap * 0.5, 0, m.ItemGap * 0.5, 0);
            outer.VerticalAlignment = VerticalAlignment.Center;

            Border card = new Border();
            card.Height = cardH;
            card.MinWidth = cardW;
            card.CornerRadius = new CornerRadius(cardH / 2.0);
            card.Background = ThemeManager.Brush(palette.CardBg);
            card.BorderBrush = ThemeManager.Brush(palette.CardBorder);
            card.BorderThickness = new Thickness(1);
            card.Padding = new Thickness(Math.Round(14 * scale), Math.Round(4 * scale), Math.Round(14 * scale), Math.Round(4 * scale));
            card.Cursor = Cursors.Hand;
            card.Tag = id;
            card.SnapsToDevicePixels = true;

            Brush normalBg = ThemeManager.Brush(palette.CardBg);
            Brush hoverBg = ThemeManager.Brush(palette.CardHoverBg);
            bool customBg = (id == "battery");

            card.MouseEnter += delegate
            {
                if (!customBg) card.Background = hoverBg;
                if (onHoverEnter != null)
                    onHoverEnter(L10n.T(cfg, def.NameTr, def.NameEn));
            };
            card.MouseLeave += delegate
            {
                if (!customBg) card.Background = normalBg;
                if (onHoverLeave != null)
                    onHoverLeave();
            };

            WidgetCardHandle handle = new WidgetCardHandle();
            handle.Id = id;
            handle.Container = new Border { Child = outer, Tag = id };

            // Populate specific widget interior + bind live refresh action
            switch (id)
            {
                case "ai_limits":
                    BuildAiLimits(card, cfg, palette, m, scale, handle, onOpenPopover);
                    break;
                case "now_playing":
                    BuildNowPlaying(card, cfg, palette, m, tel, scale, handle);
                    break;
                case "system_chart":
                    BuildSystemChart(card, cfg, palette, m, tel, scale, handle, onOpenPopover);
                    break;
                case "cpu_ring":
                    BuildRingGauge(card, "CPU", palette, m, scale, handle,
                        delegate { return tel.CpuPercent; },
                        delegate { return tel.CpuPercent + "%"; },
                        delegate { onOpenPopover("system_chart", card); });
                    break;
                case "ram_ring":
                    BuildRingGauge(card, "RAM", palette, m, scale, handle,
                        delegate { return tel.RamPercent; },
                        delegate { return tel.RamPercent + "%"; },
                        delegate { onOpenPopover("system_chart", card); });
                    break;
                case "storage":
                    BuildStorage(card, cfg, palette, m, tel, scale, handle, onOpenPopover);
                    break;
                case "network":
                    BuildNetwork(card, palette, m, tel, scale, handle);
                    break;
                case "battery":
                    BuildBatteryPill(card, cfg, palette, m, tel, scale, handle, onOpenPopover);
                    break;
                case "volume":
                    BuildVolume(card, cfg, palette, m, tel, scale, handle, onOpenPopover);
                    break;
                case "quick_toggles":
                    BuildQuickToggles(card, cfg, palette, m, tel, scale, handle);
                    break;
                case "recycle_bin":
                    BuildRecycleBin(card, cfg, palette, m, tel, scale, handle);
                    break;
                case "pomodoro":
                    BuildPomodoro(card, cfg, palette, m, tel, scale, handle, onOpenPopover);
                    break;
                case "screen_time":
                    BuildScreenTime(card, cfg, palette, m, tel, scale, handle, onOpenPopover);
                    break;
                case "todos":
                    BuildTodos(card, cfg, palette, m, scale, handle, onOpenPopover);
                    break;
                case "notes":
                    BuildNotes(card, cfg, palette, m, scale, handle, onOpenPopover);
                    break;
                case "clipboard":
                    BuildClipboard(card, cfg, palette, m, tel, scale, handle, onOpenPopover);
                    break;
                case "workspaces":
                    BuildWorkspaces(card, cfg, palette, m, scale, handle, onOpenPopover);
                    break;
                case "control_center":
                    BuildControlCenterTile(card, cfg, palette, m, scale, handle, onOpenPopover);
                    break;
                case "spotlight":
                    BuildSpotlightTile(card, cfg, palette, m, scale, handle, onOpenPopover);
                    break;
                case "stopwatch":
                    BuildStopwatch(card, palette, m, tel, scale, handle);
                    break;
                case "day_timeline":
                    BuildDayTimeline(card, cfg, palette, m, scale, handle);
                    break;
                case "weather":
                    BuildWeather(card, cfg, palette, m, tel, scale, handle, onOpenPopover);
                    break;
                case "crypto":
                    BuildCrypto(card, cfg, palette, m, tel, scale, handle, onOpenPopover);
                    break;
                case "today":
                    BuildToday(card, cfg, palette, m, tel, scale, handle, onOpenPopover);
                    break;
                case "flip_clock":
                    BuildFlipClock(card, cfg, palette, m, scale, handle, onOpenPopover);
                    break;
                case "analog_clock":
                    BuildAnalogClock(card, palette, m, scale, handle, onOpenPopover);
                    break;
                case "world_clocks":
                    BuildWorldClocks(card, cfg, palette, m, scale, handle);
                    break;
                case "github":
                    BuildGitHub(card, cfg, palette, m, tel, scale, handle);
                    break;
                case "commands":
                    BuildQuickCommands(card, cfg, palette, m, scale, handle, onOpenPopover);
                    break;
            }

            outer.Children.Add(card);

            // Red '-' Remove badge shown in Edit Mode (when Widget Picker is open!)
            Border badge = new Border();
            badge.Width = 17;
            badge.Height = 17;
            badge.CornerRadius = new CornerRadius(9);
            badge.Background = ThemeManager.Brush(Color.FromRgb(239, 68, 68));
            badge.BorderBrush = ThemeManager.Brush(Colors.White);
            badge.BorderThickness = new Thickness(1.2);
            badge.HorizontalAlignment = HorizontalAlignment.Right;
            badge.VerticalAlignment = VerticalAlignment.Top;
            badge.Margin = new Thickness(0, -4, -4, 0);
            badge.Cursor = Cursors.Hand;
            badge.Visibility = editMode ? Visibility.Visible : Visibility.Collapsed;
            badge.Child = new TextBlock
            {
                Text = "\u2212",
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0)
            };
            badge.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (onRemoveWidget != null) onRemoveWidget(id);
            };
            outer.Children.Add(badge);
            handle.RemoveBadge = badge;

            if (handle.RefreshAction != null)
                handle.RefreshAction();

            return handle;
        }

        // ====== 1. AI PLAN USAGE (Monolith signature!) ======
        private static void BuildAiLimits(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            Grid root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border iconPill = new Border
            {
                Width = Math.Round(28 * scale),
                Height = Math.Round(28 * scale),
                CornerRadius = new CornerRadius(Math.Round(14 * scale)),
                Background = ThemeManager.Brush(Color.FromRgb(217, 119, 87)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            iconPill.Child = new TextBlock
            {
                Text = "\u273B",
                Foreground = Brushes.White,
                FontSize = Math.Round(14 * scale),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconPill, 0);
            root.Children.Add(iconPill);

            StackPanel rows = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(rows, 1);
            root.Children.Add(rows);

            card.Child = root;
            card.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e) { openPop("ai_limits", card); };

            h.RefreshAction = delegate
            {
                rows.Children.Clear();
                int count = Math.Min(3, cfg.AiQuotas.Count);
                for (int i = 0; i < count; i++)
                {
                    AiQuotaItem q = cfg.AiQuotas[i];
                    Grid r = new Grid { Margin = new Thickness(0, i == 0 ? 0 : 2, 0, 0) };
                    r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(44 * scale)) });
                    r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Round(46 * scale)) });
                    r.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    TextBlock lbl = new TextBlock
                    {
                        Text = q.Provider,
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = Math.Max(8.5, 9.2 * scale),
                        FontWeight = FontWeights.SemiBold,
                        Foreground = ThemeManager.Brush(p.TextPrimary),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    Grid.SetColumn(lbl, 0);
                    r.Children.Add(lbl);

                    Border track = new Border
                    {
                        Height = Math.Max(4, Math.Round(4.5 * scale)),
                        CornerRadius = new CornerRadius(2.5),
                        Background = ThemeManager.Brush(Color.FromArgb(45, p.IsLight ? (byte)0 : (byte)255, p.IsLight ? (byte)0 : (byte)255, p.IsLight ? (byte)0 : (byte)255)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(3, 0, 5, 0)
                    };
                    Color barCol = q.PercentUsed >= 85 ? Color.FromRgb(239, 68, 68) :
                                   q.PercentUsed >= 65 ? Color.FromRgb(245, 158, 11) :
                                   Color.FromRgb(34, 197, 94);
                    Border fill = new Border
                    {
                        Height = track.Height,
                        CornerRadius = new CornerRadius(2.5),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Width = Math.Max(4, Math.Round(38 * scale * Math.Min(100, Math.Max(0, q.PercentUsed)) / 100.0)),
                        Background = ThemeManager.Brush(barCol)
                    };
                    track.Child = fill;
                    Grid.SetColumn(track, 1);
                    r.Children.Add(track);

                    TextBlock pct = new TextBlock
                    {
                        Text = q.PercentUsed + "%",
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = Math.Max(8.5, 9.0 * scale),
                        FontWeight = FontWeights.SemiBold,
                        Foreground = ThemeManager.Brush(p.TextSecondary),
                        VerticalAlignment = VerticalAlignment.Center,
                        MinWidth = 24,
                        TextAlignment = TextAlignment.Right
                    };
                    Grid.SetColumn(pct, 2);
                    r.Children.Add(pct);

                    rows.Children.Add(r);
                }
            };
        }

        // ====== 2. NOW PLAYING (Monolith Media Capsule) ======
        private static void BuildNowPlaying(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h)
        {
            Grid root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Album circular Spatial orb
            Border art = new Border
            {
                Width = Math.Round(32 * scale),
                Height = Math.Round(32 * scale),
                CornerRadius = new CornerRadius(Math.Round(16 * scale)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            LinearGradientBrush artGrad = new LinearGradientBrush(
                Color.FromRgb(236, 72, 153),
                Color.FromRgb(99, 102, 241),
                45);
            artGrad.Freeze();
            art.Background = artGrad;
            art.Child = new TextBlock
            {
                Text = "\u266B",
                Foreground = Brushes.White,
                FontSize = Math.Round(15 * scale),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(art, 0);
            root.Children.Add(art);

            StackPanel meta = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = Math.Round(96 * scale) };
            TextBlock titleTxt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(11.0 * scale),
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(p.TextPrimary),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            TextBlock subTxt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(9.5 * scale),
                Foreground = ThemeManager.Brush(p.TextMuted),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            meta.Children.Add(titleTxt);
            meta.Children.Add(subTxt);
            Grid.SetColumn(meta, 1);
            root.Children.Add(meta);

            StackPanel ctrls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            ctrls.Children.Add(MakeMediaBtn("\uE892", false, p, scale, delegate { NativeWin32.SendKeyPress(NativeWin32.VK_MEDIA_PREV_TRACK); }));
            ctrls.Children.Add(MakeMediaBtn("\uE768", true, p, scale, delegate { NativeWin32.SendKeyPress(NativeWin32.VK_MEDIA_PLAY_PAUSE); }));
            ctrls.Children.Add(MakeMediaBtn("\uE893", false, p, scale, delegate { NativeWin32.SendKeyPress(NativeWin32.VK_MEDIA_NEXT_TRACK); }));
            Grid.SetColumn(ctrls, 2);
            root.Children.Add(ctrls);

            card.Child = root;

            h.RefreshAction = delegate
            {
                titleTxt.Text = tel.Media.HasMedia ? tel.Media.Title : L10n.T(cfg, "Medya Hazır", "Ready to Play");
                subTxt.Text = tel.Media.SourceApp;
            };
        }

        private static Border MakeMediaBtn(string glyph, bool primary, ThemePalette p, double scale, Action click)
        {
            double sz = primary ? Math.Round(24 * scale) : Math.Round(20 * scale);
            Border b = new Border
            {
                Width = sz,
                Height = sz,
                CornerRadius = new CornerRadius(sz / 2.0),
                Margin = new Thickness(2, 0, 2, 0),
                Background = primary
                    ? ThemeManager.Brush(Color.FromArgb(65, 255, 255, 255))
                    : Brushes.Transparent,
                Cursor = Cursors.Hand
            };
            if (primary && p.IsLight)
                b.Background = ThemeManager.Brush(Color.FromArgb(30, 15, 23, 42));

            b.Child = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = primary ? Math.Round(10.5 * scale) : Math.Round(9.5 * scale),
                Foreground = ThemeManager.Brush(p.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            b.MouseEnter += delegate { b.Background = ThemeManager.Brush(Color.FromArgb(95, p.Accent.R, p.Accent.G, p.Accent.B)); };
            b.MouseLeave += delegate
            {
                b.Background = primary
                    ? ThemeManager.Brush(p.IsLight ? Color.FromArgb(30, 15, 23, 42) : Color.FromArgb(65, 255, 255, 255))
                    : Brushes.Transparent;
            };
            b.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                click();
            };
            return b;
        }

        // ====== 3. CPU & RAM LIVE SPARKLINE ======
        private static void BuildSystemChart(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            DockPanel topRow = new DockPanel();
            TextBlock cpuTxt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(10.5 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(p.TextPrimary)
            };
            TextBlock ramTxt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(10.0 * scale),
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(p.TextSecondary),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(cpuTxt, Dock.Left);
            DockPanel.SetDock(ramTxt, Dock.Right);
            topRow.Children.Add(cpuTxt);
            topRow.Children.Add(ramTxt);
            Grid.SetRow(topRow, 0);
            root.Children.Add(topRow);

            double chartW = Math.Round(114 * scale);
            double chartH = Math.Round(20 * scale);
            Canvas sparkCanvas = new Canvas { Width = chartW, Height = chartH, Margin = new Thickness(4, 3, 4, 0), ClipToBounds = true };
            Polygon fillPoly = new Polygon();
            LinearGradientBrush areaGrad = new LinearGradientBrush(
                Color.FromArgb(115, p.Accent.R, p.Accent.G, p.Accent.B),
                Color.FromArgb(8, p.Accent.R, p.Accent.G, p.Accent.B),
                90);
            areaGrad.Freeze();
            fillPoly.Fill = areaGrad;
            Polyline strokeLine = new Polyline
            {
                Stroke = ThemeManager.Brush(p.Accent),
                StrokeThickness = 1.6,
                StrokeLineJoin = PenLineJoin.Round
            };
            sparkCanvas.Children.Add(fillPoly);
            sparkCanvas.Children.Add(strokeLine);
            Grid.SetRow(sparkCanvas, 1);
            root.Children.Add(sparkCanvas);

            card.Child = root;
            card.MouseLeftButtonUp += delegate { openPop("system_chart", card); };

            h.RefreshAction = delegate
            {
                cpuTxt.Text = "CPU " + tel.CpuPercent + "%";
                ramTxt.Text = "RAM " + tel.RamPercent + "%";

                PointCollection linePts = new PointCollection();
                PointCollection areaPts = new PointCollection();
                int n = tel.CpuHistory.Count;
                if (n > 1)
                {
                    areaPts.Add(new Point(0, chartH));
                    for (int i = 0; i < n; i++)
                    {
                        double x = (double)i / (n - 1) * chartW;
                        double val = Math.Max(4, Math.Min(100, tel.CpuHistory[i]));
                        double y = chartH - (val / 100.0) * (chartH - 2);
                        linePts.Add(new Point(x, y));
                        areaPts.Add(new Point(x, y));
                    }
                    areaPts.Add(new Point(chartW, chartH));
                }
                strokeLine.Points = linePts;
                fillPoly.Points = areaPts;
            };
        }

        // ====== 4 & 5. CIRCULAR RING GAUGES (CPU / RAM) ======
        private static void BuildRingGauge(Border card, string label, ThemePalette p, DockMetrics m, double scale, WidgetCardHandle h, Func<int> getPct, Func<string> getText, Action onClick)
        {
            double sz = Math.Round(34 * scale);
            Grid g = new Grid { Width = sz, Height = sz, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

            Ellipse track = new Ellipse
            {
                Width = sz,
                Height = sz,
                Stroke = ThemeManager.Brush(Color.FromArgb(45, p.IsLight ? (byte)0 : (byte)255, p.IsLight ? (byte)0 : (byte)255, p.IsLight ? (byte)0 : (byte)255)),
                StrokeThickness = 3.2
            };
            g.Children.Add(track);

            System.Windows.Shapes.Path arcPath = new System.Windows.Shapes.Path
            {
                StrokeThickness = 3.2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            g.Children.Add(arcPath);

            StackPanel center = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            TextBlock valTxt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(9.5 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(p.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            TextBlock lblTxt = new TextBlock
            {
                Text = label,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(7.2 * scale),
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(p.TextMuted),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, -2, 0, 0)
            };
            center.Children.Add(valTxt);
            center.Children.Add(lblTxt);
            g.Children.Add(center);

            card.Padding = new Thickness(4);
            card.Child = g;
            card.MouseLeftButtonUp += delegate { onClick(); };

            h.RefreshAction = delegate
            {
                int pct = Math.Max(0, Math.Min(100, getPct()));
                valTxt.Text = getText();
                Color c = pct >= 85 ? Color.FromRgb(239, 68, 68) :
                          pct >= 65 ? Color.FromRgb(249, 115, 22) : p.Accent;
                arcPath.Stroke = ThemeManager.Brush(c);
                arcPath.Data = BuildArcGeometry(sz / 2.0, sz / 2.0, (sz - 4) / 2.0, pct / 100.0);
            };
        }

        public static Geometry BuildArcGeometry(double cx, double cy, double r, double fraction)
        {
            if (fraction <= 0.01) return Geometry.Empty;
            if (fraction >= 0.995) fraction = 0.995;
            double angle = fraction * 360.0;
            double startRad = -Math.PI / 2.0;
            double endRad = startRad + angle * Math.PI / 180.0;
            Point startPt = new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
            Point endPt = new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad));
            bool largeArc = angle > 180.0;

            StreamGeometry sg = new StreamGeometry();
            using (StreamGeometryContext ctx = sg.Open())
            {
                ctx.BeginFigure(startPt, false, false);
                ctx.ArcTo(endPt, new Size(r, r), 0, largeArc, SweepDirection.Clockwise, true, false);
            }
            sg.Freeze();
            return sg;
        }

        // ====== 6. STORAGE CARD (Exact Monolith screenshot design!) ======
        private static void BuildStorage(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            DockPanel r0 = new DockPanel();
            TextBlock title = new TextBlock
            {
                Text = L10n.T(cfg, "Depolama", "Storage"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(9.5 * scale),
                Foreground = ThemeManager.Brush(p.TextMuted)
            };
            TextBlock pctTxt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(9.5 * scale),
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(p.TextSecondary),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(title, Dock.Left);
            DockPanel.SetDock(pctTxt, Dock.Right);
            r0.Children.Add(title);
            r0.Children.Add(pctTxt);
            Grid.SetRow(r0, 0);
            root.Children.Add(r0);

            StackPanel r1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 3) };
            TextBlock freeBig = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(13.0 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(p.TextPrimary),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            TextBlock totalSub = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(9.0 * scale),
                Foreground = ThemeManager.Brush(p.TextMuted),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(4, 0, 0, 1)
            };
            r1.Children.Add(freeBig);
            r1.Children.Add(totalSub);
            Grid.SetRow(r1, 1);
            root.Children.Add(r1);

            double barW = Math.Round(118 * scale);
            Border track = new Border
            {
                Width = barW,
                Height = Math.Max(4, Math.Round(4.5 * scale)),
                CornerRadius = new CornerRadius(2.5),
                Background = ThemeManager.Brush(Color.FromArgb(45, p.IsLight ? (byte)0 : (byte)255, p.IsLight ? (byte)0 : (byte)255, p.IsLight ? (byte)0 : (byte)255))
            };
            Border fill = new Border
            {
                Height = track.Height,
                CornerRadius = new CornerRadius(2.5),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            track.Child = fill;
            Grid.SetRow(track, 2);
            root.Children.Add(track);

            card.Child = root;
            card.MouseLeftButtonUp += delegate { openPop("storage", card); };

            h.RefreshAction = delegate
            {
                if (tel.Drives.Count == 0) return;
                DriveStat d = tel.Drives[0];
                pctTxt.Text = d.UsedPercent + "%";
                freeBig.Text = string.Format("{0:0} GB", d.FreeGB);
                totalSub.Text = string.Format(L10n.T(cfg, "/ {0:0} GB boş", "free of {0:0} GB"), d.TotalGB);
                fill.Width = Math.Max(4, barW * Math.Min(100, d.UsedPercent) / 100.0);
                Color barCol = d.UsedPercent >= 88 ? Color.FromRgb(239, 68, 68) :
                               d.UsedPercent >= 70 ? Color.FromRgb(249, 115, 22) :
                               p.Accent;
                fill.Background = ThemeManager.Brush(barCol);
            };
        }

        // ====== 7. NETWORK SPEED ======
        private static void BuildNetwork(Border card, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h)
        {
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock downTxt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(11.0 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(Color.FromRgb(56, 189, 248))
            };
            TextBlock upTxt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(10.0 * scale),
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(Color.FromRgb(52, 211, 153)),
                Margin = new Thickness(0, 1, 0, 0)
            };
            sp.Children.Add(downTxt);
            sp.Children.Add(upTxt);
            card.Child = sp;

            card.MouseLeftButtonUp += delegate
            {
                try { Process.Start(new ProcessStartInfo("ms-settings:network-status") { UseShellExecute = true }); } catch { }
            };

            h.RefreshAction = delegate
            {
                downTxt.Text = "\u2193 " + SystemTelemetry.FormatSpeed(tel.NetDownBps);
                upTxt.Text = "\u2191 " + SystemTelemetry.FormatSpeed(tel.NetUpBps);
            };
        }

        // ====== 8. BATTERY PILL (Monolith bright blue pill from screenshot!) ======
        private static void BuildBatteryPill(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            StackPanel sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock icon = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = Math.Round(13.0 * scale),
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0)
            };
            TextBlock pct = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(14.0 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            sp.Children.Add(icon);
            sp.Children.Add(pct);
            card.Child = sp;
            card.MouseLeftButtonUp += delegate { openPop("battery", card); };

            h.RefreshAction = delegate
            {
                Color pillColor;
                if (!tel.HasBattery)
                {
                    pillColor = p.Accent;
                    icon.Text = "\u26A1";
                    pct.Text = "AC";
                }
                else
                {
                    icon.Text = tel.IsCharging ? "\u26A1" : "\u2022";
                    pct.Text = tel.BatteryPercent.ToString();
                    if (tel.BatteryPercent <= 20 && !tel.IsCharging)
                        pillColor = Color.FromRgb(239, 68, 68);
                    else if (tel.IsCharging)
                        pillColor = Color.FromRgb(34, 197, 94);
                    else
                        pillColor = Color.FromRgb(37, 99, 235);
                }
                LinearGradientBrush lg = new LinearGradientBrush(
                    Color.FromArgb(240, (byte)Math.Min(255, pillColor.R + 22), (byte)Math.Min(255, pillColor.G + 22), (byte)Math.Min(255, pillColor.B + 22)),
                    Color.FromArgb(240, pillColor.R, pillColor.G, pillColor.B),
                    90);
                lg.Freeze();
                card.Background = lg;
                card.BorderBrush = ThemeManager.Brush(Color.FromArgb(100, 255, 255, 255));
            };
        }

        // ====== 9. AUDIO VOLUME (Real Core Audio + Wheel Scroll!) ======
        private static void BuildVolume(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            StackPanel top = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            TextBlock glyph = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = Math.Round(12.5 * scale),
                Foreground = ThemeManager.Brush(p.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            TextBlock val = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(12.0 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(p.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center
            };
            top.Children.Add(glyph);
            top.Children.Add(val);

            double barW = Math.Round(60 * scale);
            Border track = new Border
            {
                Width = barW,
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Margin = new Thickness(0, 4, 0, 0),
                Background = ThemeManager.Brush(Color.FromArgb(45, p.IsLight ? (byte)0 : (byte)255, p.IsLight ? (byte)0 : (byte)255, p.IsLight ? (byte)0 : (byte)255))
            };
            Border fill = new Border
            {
                Height = 4,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = ThemeManager.Brush(p.Accent)
            };
            track.Child = fill;
            sp.Children.Add(top);
            sp.Children.Add(track);
            card.Child = sp;

            card.MouseWheel += delegate(object s, MouseWheelEventArgs e)
            {
                int step = e.Delta > 0 ? 4 : -4;
                int next = Math.Max(0, Math.Min(100, tel.VolumePercent + step));
                NativeWin32.SetSystemVolume(next);
                tel.VolumePercent = next;
                if (h.RefreshAction != null) h.RefreshAction();
                e.Handled = true;
            };
            card.MouseLeftButtonUp += delegate { openPop("volume", card); };
            card.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                NativeWin32.ToggleSystemMute();
                tel.VolumeMuted = !tel.VolumeMuted;
                if (h.RefreshAction != null) h.RefreshAction();
            };

            h.RefreshAction = delegate
            {
                glyph.Text = tel.VolumeMuted || tel.VolumePercent == 0 ? "\uE74F" :
                             tel.VolumePercent < 40 ? "\uE993" : "\uE767";
                val.Text = tel.VolumeMuted ? "MUTE" : (tel.VolumePercent + "%");
                fill.Width = tel.VolumeMuted ? 0 : Math.Max(3, barW * tel.VolumePercent / 100.0);
            };
        }

        // ====== 10. QUICK TOGGLES ======
        private static void BuildQuickToggles(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            row.Children.Add(MakeToggleChip("\uE701", p, scale, delegate { try { Process.Start(new ProcessStartInfo("ms-settings:network-wifi") { UseShellExecute = true }); } catch { } }));
            row.Children.Add(MakeToggleChip("\uE702", p, scale, delegate { try { Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true }); } catch { } }));
            row.Children.Add(MakeToggleChip("\uE74F", p, scale, delegate { NativeWin32.ToggleSystemMute(); }));
            row.Children.Add(MakeToggleChip("\uE72E", p, scale, delegate { NativeWin32.LockWorkStation(); }));
            card.Padding = new Thickness(5, 3, 5, 3);
            card.Child = row;
        }

        private static Border MakeToggleChip(string glyph, ThemePalette p, double scale, Action click)
        {
            double sz = Math.Round(25 * scale);
            Border b = new Border
            {
                Width = sz,
                Height = sz,
                CornerRadius = new CornerRadius(sz / 2.0),
                Margin = new Thickness(2.5, 0, 2.5, 0),
                Background = ThemeManager.Brush(Color.FromArgb(40, p.IsLight ? (byte)0 : (byte)255, p.IsLight ? (byte)0 : (byte)255, p.IsLight ? (byte)0 : (byte)255)),
                Cursor = Cursors.Hand
            };
            b.Child = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = Math.Round(11.5 * scale),
                Foreground = ThemeManager.Brush(p.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            b.MouseEnter += delegate { b.Background = ThemeManager.Brush(p.Accent); };
            b.MouseLeave += delegate { b.Background = ThemeManager.Brush(Color.FromArgb(32, p.IsLight ? (byte)0 : (byte)255, p.IsLight ? (byte)0 : (byte)255, p.IsLight ? (byte)0 : (byte)255)); };
            b.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; click(); };
            return b;
        }

        // ====== 11. RECYCLE BIN ======
        private static void BuildRecycleBin(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h)
        {
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            TextBlock top = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Text = "\uE74D",
                FontSize = Math.Round(14.5 * scale),
                Foreground = ThemeManager.Brush(p.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            TextBlock sub = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(9.5 * scale),
                Foreground = ThemeManager.Brush(p.TextMuted),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            sp.Children.Add(top);
            sp.Children.Add(sub);
            card.Child = sp;

            card.MouseLeftButtonUp += delegate
            {
                try { Process.Start(new ProcessStartInfo("shell:RecycleBinFolder") { UseShellExecute = true }); } catch { }
            };
            card.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                try { NativeWin32.SHEmptyRecycleBin(IntPtr.Zero, null, 0); tel.RefreshRecycleBin(); if (h.RefreshAction != null) h.RefreshAction(); } catch { }
            };

            h.RefreshAction = delegate
            {
                sub.Text = tel.RecycleItems == 0 ? L10n.T(cfg, "Boş", "Empty") : (tel.RecycleItems + " " + L10n.T(cfg, "öğe", "items"));
            };
        }

        // ====== 12. POMODORO FOCUS ======
        private static void BuildPomodoro(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            double ringSz = Math.Round(28 * scale);
            Grid ring = new Grid { Width = ringSz, Height = ringSz, Margin = new Thickness(0, 0, 7, 0) };
            ring.Children.Add(new Ellipse
            {
                Width = ringSz,
                Height = ringSz,
                Stroke = ThemeManager.Brush(Color.FromArgb(45, 239, 68, 68)),
                StrokeThickness = 3
            });
            System.Windows.Shapes.Path arc = new System.Windows.Shapes.Path
            {
                Stroke = ThemeManager.Brush(Color.FromRgb(239, 68, 68)),
                StrokeThickness = 3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            ring.Children.Add(arc);
            TextBlock playGlyph = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = Math.Round(9.5 * scale),
                Foreground = ThemeManager.Brush(Color.FromRgb(239, 68, 68)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ring.Children.Add(playGlyph);
            ring.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                tel.PomodoroRunning = !tel.PomodoroRunning;
                if (h.RefreshAction != null) h.RefreshAction();
            };

            StackPanel meta = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock phaseTxt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(8.5 * scale),
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(Color.FromRgb(248, 113, 113))
            };
            TextBlock timeTxt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(13.5 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(p.TextPrimary)
            };
            meta.Children.Add(phaseTxt);
            meta.Children.Add(timeTxt);

            row.Children.Add(ring);
            row.Children.Add(meta);
            card.Child = row;
            card.MouseLeftButtonUp += delegate { openPop("pomodoro", card); };

            h.RefreshAction = delegate
            {
                int mins = tel.PomodoroSecondsLeft / 60;
                int secs = tel.PomodoroSecondsLeft % 60;
                timeTxt.Text = string.Format("{0:00}:{1:00}", mins, secs);
                phaseTxt.Text = tel.PomodoroPhase.ToUpperInvariant();
                playGlyph.Text = tel.PomodoroRunning ? "\uE769" : "\uE768";
                double frac = tel.PomodoroTotalSeconds > 0 ? (double)tel.PomodoroSecondsLeft / tel.PomodoroTotalSeconds : 1.0;
                arc.Data = BuildArcGeometry(ringSz / 2.0, ringSz / 2.0, (ringSz - 4) / 2.0, frac);
            };
        }

        // ====== 13. SCREEN TIME (Live Local Bar Chart!) ======
        private static void BuildScreenTime(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            StackPanel left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            TextBlock lbl = new TextBlock
            {
                Text = L10n.T(cfg, "Ekran", "Screen"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(8.5 * scale),
                Foreground = ThemeManager.Brush(p.TextMuted)
            };
            TextBlock dur = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(13.0 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(p.TextPrimary)
            };
            left.Children.Add(lbl);
            left.Children.Add(dur);

            StackPanel bars = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = Math.Round(28 * scale)
            };
            row.Children.Add(left);
            row.Children.Add(bars);
            card.Child = row;
            card.MouseLeftButtonUp += delegate { openPop("screen_time", card); };

            h.RefreshAction = delegate
            {
                int total = tel.GetTotalScreenTimeSeconds(cfg);
                dur.Text = SystemTelemetry.FormatDuration(total);
                bars.Children.Clear();

                int curHr = DateTime.Now.Hour;
                int startHr = Math.Max(0, curHr - 9);
                int maxVal = 60;
                for (int hr = startHr; hr <= Math.Min(23, startHr + 9); hr++)
                    if (cfg.ScreenTimeByHour[hr] > maxVal) maxVal = cfg.ScreenTimeByHour[hr];

                for (int i = 0; i < 10; i++)
                {
                    int hr = startHr + i;
                    int sec = (hr < 24) ? cfg.ScreenTimeByHour[hr] : 0;
                    double barH = Math.Max(3, Math.Round((double)sec / maxVal * 26.0 * scale));
                    Border bar = new Border
                    {
                        Width = Math.Max(3, Math.Round(3.5 * scale)),
                        Height = barH,
                        CornerRadius = new CornerRadius(1.5),
                        Margin = new Thickness(1, 0, 1, 0),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Background = ThemeManager.Brush(hr == curHr ? p.Accent : Color.FromArgb(145, p.Accent.R, p.Accent.G, p.Accent.B))
                    };
                    bars.Children.Add(bar);
                }
            };
        }

        // ====== 14. TODOS ======
        private static void BuildTodos(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            double rsz = Math.Round(24 * scale);
            Grid ring = new Grid { Width = rsz, Height = rsz, Margin = new Thickness(0, 0, 7, 0) };
            ring.Children.Add(new Ellipse
            {
                Width = rsz,
                Height = rsz,
                Stroke = ThemeManager.Brush(Color.FromArgb(45, 34, 197, 94)),
                StrokeThickness = 2.6
            });
            System.Windows.Shapes.Path arc = new System.Windows.Shapes.Path
            {
                Stroke = ThemeManager.Brush(Color.FromRgb(34, 197, 94)),
                StrokeThickness = 2.6,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            ring.Children.Add(arc);
            TextBlock countTxt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(8.5 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(p.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ring.Children.Add(countTxt);

            StackPanel meta = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = Math.Round(96 * scale) };
            TextBlock hdr = new TextBlock
            {
                Text = L10n.T(cfg, "GÖREVLER", "TODOS"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(8.2 * scale),
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(Color.FromRgb(52, 211, 153))
            };
            TextBlock nextTxt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(10.5 * scale),
                FontWeight = FontWeights.Medium,
                Foreground = ThemeManager.Brush(p.TextPrimary),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            meta.Children.Add(hdr);
            meta.Children.Add(nextTxt);

            row.Children.Add(ring);
            row.Children.Add(meta);
            card.Child = row;
            card.MouseLeftButtonUp += delegate { openPop("todos", card); };

            h.RefreshAction = delegate
            {
                int done = 0;
                string firstOpen = null;
                for (int i = 0; i < cfg.Todos.Count; i++)
                {
                    if (cfg.Todos[i].Done) done++;
                    else if (firstOpen == null) firstOpen = cfg.Todos[i].Text;
                }
                int total = cfg.Todos.Count;
                countTxt.Text = done + "/" + total;
                nextTxt.Text = firstOpen != null ? firstOpen : L10n.T(cfg, "Tümü tamam!", "All done!");
                double frac = total > 0 ? (double)done / total : 1.0;
                arc.Data = BuildArcGeometry(rsz / 2.0, rsz / 2.0, (rsz - 4) / 2.0, frac);
            };
        }

        // ====== 15. QUICK NOTES ======
        private static void BuildNotes(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = Math.Round(100 * scale) };
            TextBlock hdr = new TextBlock
            {
                Text = "\u270E " + L10n.T(cfg, "NOTLAR", "NOTES"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(8.5 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(Color.FromRgb(250, 204, 21))
            };
            TextBlock preview = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(10.5 * scale),
                Foreground = ThemeManager.Brush(p.TextPrimary),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            sp.Children.Add(hdr);
            sp.Children.Add(preview);
            card.Child = sp;
            card.MouseLeftButtonUp += delegate { openPop("notes", card); };

            h.RefreshAction = delegate
            {
                string t = (cfg.NotesText ?? "").Trim();
                int nl = t.IndexOf('\n');
                if (nl > 0) t = t.Substring(0, nl).Trim();
                preview.Text = string.IsNullOrEmpty(t) ? L10n.T(cfg, "Not eklemek icin tikla", "Click to write...") : t;
            };
        }

        // ====== 16. STOPWATCH ======
        private static void BuildStopwatch(Border card, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock timeTxt = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = Math.Round(13.0 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(p.TextPrimary),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Border toggle = MakeMediaBtn("\uE768", true, p, scale, delegate
            {
                if (tel.StopwatchRunning) { tel.StopwatchTimer.Stop(); tel.StopwatchRunning = false; }
                else { tel.StopwatchTimer.Start(); tel.StopwatchRunning = true; }
                if (h.RefreshAction != null) h.RefreshAction();
            });
            row.Children.Add(timeTxt);
            row.Children.Add(toggle);
            card.Child = row;

            card.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                tel.StopwatchTimer.Reset();
                tel.StopwatchRunning = false;
                if (h.RefreshAction != null) h.RefreshAction();
            };

            h.RefreshAction = delegate
            {
                TimeSpan ts = tel.StopwatchTimer.Elapsed;
                timeTxt.Text = string.Format("{0:00}:{1:00}", (int)ts.TotalMinutes, ts.Seconds);
                TextBlock tb = toggle.Child as TextBlock;
                if (tb != null) tb.Text = tel.StopwatchRunning ? "\uE769" : "\uE768";
            };
        }

        // ====== 17. DAY TIMELINE ======
        private static void BuildDayTimeline(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, double scale, WidgetCardHandle h)
        {
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            DockPanel hdr = new DockPanel();
            TextBlock lbl = new TextBlock
            {
                Text = L10n.T(cfg, "Gün Akışı", "Day Timeline"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(8.8 * scale),
                Foreground = ThemeManager.Brush(p.TextMuted)
            };
            TextBlock pctTxt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(8.8 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(p.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(lbl, Dock.Left);
            DockPanel.SetDock(pctTxt, Dock.Right);
            hdr.Children.Add(lbl);
            hdr.Children.Add(pctTxt);

            double barW = Math.Round(110 * scale);
            Grid track = new Grid { Width = barW, Height = Math.Round(11 * scale), Margin = new Thickness(0, 4, 0, 0) };
            StackPanel segs = new StackPanel { Orientation = Orientation.Horizontal };
            segs.Children.Add(new Border { Width = barW * 0.28, Height = 8, CornerRadius = new CornerRadius(4, 0, 0, 4), Background = ThemeManager.Brush(Color.FromRgb(56, 189, 248)) });
            segs.Children.Add(new Border { Width = barW * 0.34, Height = 8, Background = ThemeManager.Brush(Color.FromRgb(168, 85, 247)) });
            segs.Children.Add(new Border { Width = barW * 0.22, Height = 8, Background = ThemeManager.Brush(Color.FromRgb(249, 115, 22)) });
            segs.Children.Add(new Border { Width = barW * 0.16, Height = 8, CornerRadius = new CornerRadius(0, 4, 4, 0), Background = ThemeManager.Brush(Color.FromRgb(59, 130, 246)) });
            track.Children.Add(segs);

            Border cursor = new Border
            {
                Width = 3,
                Height = Math.Round(12 * scale),
                CornerRadius = new CornerRadius(1.5),
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            track.Children.Add(cursor);

            sp.Children.Add(hdr);
            sp.Children.Add(track);
            card.Child = sp;

            h.RefreshAction = delegate
            {
                DateTime now = DateTime.Now;
                double frac = (now.Hour * 60 + now.Minute) / 1440.0;
                pctTxt.Text = (int)Math.Round(frac * 100) + "%";
                cursor.Margin = new Thickness(Math.Max(0, Math.Min(barW - 3, frac * barW)), 0, 0, 0);
            };
        }

        // ====== 18. WEATHER ======
        private static Color GetWeatherIconColor(int code)
        {
            if (code == 0) return Color.FromRgb(251, 191, 36);      // Sunny gold
            if (code <= 2) return Color.FromRgb(253, 224, 71);      // Partly cloudy warm sun
            if (code == 3) return Color.FromRgb(148, 197, 255);     // Cloudy soft sky blue
            if (code <= 67) return Color.FromRgb(56, 189, 248);     // Rain cyan
            if (code <= 77) return Color.FromRgb(224, 242, 254);    // Snow ice white
            return Color.FromRgb(250, 204, 21);                     // Storm yellow
        }

        private static void BuildWeather(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock icon = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI Symbol"),
                FontSize = Math.Round(18.0 * scale),
                Foreground = ThemeManager.Brush(GetWeatherIconColor(tel.Weather.WeatherCode)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            StackPanel meta = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock temp = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(13.5 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(p.TextPrimary)
            };
            TextBlock city = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(9.0 * scale),
                Foreground = ThemeManager.Brush(p.TextMuted),
                MaxWidth = Math.Round(60 * scale),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            meta.Children.Add(temp);
            meta.Children.Add(city);
            row.Children.Add(icon);
            row.Children.Add(meta);
            card.Child = row;
            card.MouseLeftButtonUp += delegate { openPop("weather", card); };

            h.RefreshAction = delegate
            {
                icon.Text = tel.Weather.IconGlyph;
                icon.Foreground = ThemeManager.Brush(GetWeatherIconColor(tel.Weather.WeatherCode));
                temp.Text = string.Format("{0:0}\u00B0", tel.Weather.TempC);
                city.Text = tel.Weather.City;
            };
        }

        // ====== 19. CRYPTO / MARKETS ======
        private static void BuildCrypto(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            DockPanel top = new DockPanel();
            TextBlock sym = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(9.5 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(Color.FromRgb(245, 158, 11))
            };
            TextBlock delta = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(9.2 * scale),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(sym, Dock.Left);
            DockPanel.SetDock(delta, Dock.Right);
            top.Children.Add(sym);
            top.Children.Add(delta);

            TextBlock price = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(13.0 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(p.TextPrimary),
                Margin = new Thickness(0, 1, 0, 0)
            };
            sp.Children.Add(top);
            sp.Children.Add(price);
            card.Child = sp;

            card.MouseLeftButtonUp += delegate { tel.RefreshOnlineAsync(cfg, true); };

            h.RefreshAction = delegate
            {
                sym.Text = tel.Market.Symbol;
                bool up = tel.Market.Change24hPct >= 0;
                delta.Text = string.Format("{0}{1:0.0}%", up ? "+" : "", tel.Market.Change24hPct);
                delta.Foreground = ThemeManager.Brush(up ? Color.FromRgb(34, 197, 94) : Color.FromRgb(239, 68, 68));
                if (tel.Market.Loaded && tel.Market.Price > 0)
                {
                    price.Text = tel.Market.Price >= 1000
                        ? string.Format(CultureInfo.InvariantCulture, "{0}{1:N0}", tel.Market.CurrencyPrefix, tel.Market.Price)
                        : string.Format(CultureInfo.InvariantCulture, "{0}{1:0.00}", tel.Market.CurrencyPrefix, tel.Market.Price);
                }
                else
                {
                    price.Text = "...";
                }
            };
        }

        // ====== 20. TODAY (CLOCK + DATE + TEMP) ======
        private static void BuildToday(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            TextBlock clock = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(15.5 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(p.TextPrimary),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            TextBlock date = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(9.5 * scale),
                Foreground = ThemeManager.Brush(p.TextSecondary),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0)
            };
            sp.Children.Add(clock);
            sp.Children.Add(date);
            card.Child = sp;
            card.MouseLeftButtonUp += delegate { openPop("calendar", card); };

            h.RefreshAction = delegate
            {
                DateTime now = DateTime.Now;
                clock.Text = cfg.Use24HourClock ? now.ToString("HH:mm") : now.ToString("hh:mm tt");
                CultureInfo ci = L10n.IsTr(cfg) ? new CultureInfo("tr-TR") : CultureInfo.InvariantCulture;
                date.Text = now.ToString("ddd d MMM", ci);
            };
        }

        // ====== 21. FLIP CLOCK ======
        private static void BuildFlipClock(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            TextBlock hhTxt;
            TextBlock mmTxt;
            Border hhBox = MakeFlipTile(p, scale, out hhTxt);
            Border mmBox = MakeFlipTile(p, scale, out mmTxt);
            TextBlock colon = new TextBlock
            {
                Text = ":",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(14 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(p.TextMuted),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, -2, 2, 0)
            };
            row.Children.Add(hhBox);
            row.Children.Add(colon);
            row.Children.Add(mmBox);
            card.Padding = new Thickness(5, 3, 5, 3);
            card.Child = row;
            card.MouseLeftButtonUp += delegate { openPop("calendar", card); };

            h.RefreshAction = delegate
            {
                DateTime now = DateTime.Now;
                int hr = cfg.Use24HourClock ? now.Hour : (now.Hour % 12 == 0 ? 12 : now.Hour % 12);
                hhTxt.Text = hr.ToString("00");
                mmTxt.Text = now.Minute.ToString("00");
            };
        }

        private static Border MakeFlipTile(ThemePalette p, double scale, out TextBlock txt)
        {
            Border b = new Border
            {
                Width = Math.Round(30 * scale),
                Height = Math.Round(32 * scale),
                CornerRadius = new CornerRadius(7),
                Background = ThemeManager.Brush(p.IsLight ? Color.FromRgb(28, 32, 42) : Color.FromRgb(12, 14, 19)),
                BorderBrush = ThemeManager.Brush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };
            Grid g = new Grid();
            txt = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = Math.Round(15.5 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            g.Children.Add(txt);
            g.Children.Add(new Border
            {
                Height = 1,
                VerticalAlignment = VerticalAlignment.Center,
                Background = ThemeManager.Brush(Color.FromArgb(140, 0, 0, 0))
            });
            b.Child = g;
            return b;
        }

        // ====== 22. ANALOG CLOCK ======
        private static void BuildAnalogClock(Border card, ThemePalette p, DockMetrics m, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            double sz = Math.Round(36 * scale);
            double c = sz / 2.0;
            Canvas cv = new Canvas { Width = sz, Height = sz };
            cv.Children.Add(new Ellipse
            {
                Width = sz,
                Height = sz,
                Stroke = ThemeManager.Brush(p.CardBorder),
                StrokeThickness = 1.4,
                Fill = ThemeManager.Brush(p.IsLight ? Color.FromArgb(20, 0, 0, 0) : Color.FromArgb(45, 0, 0, 0))
            });

            Line hrHand = new Line { X1 = c, Y1 = c, Stroke = ThemeManager.Brush(p.TextPrimary), StrokeThickness = 2.0, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            Line minHand = new Line { X1 = c, Y1 = c, Stroke = ThemeManager.Brush(p.TextPrimary), StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            Line secHand = new Line { X1 = c, Y1 = c, Stroke = ThemeManager.Brush(p.Accent), StrokeThickness = 1.0, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            cv.Children.Add(hrHand);
            cv.Children.Add(minHand);
            cv.Children.Add(secHand);
            Ellipse dot = new Ellipse { Width = 4, Height = 4, Fill = ThemeManager.Brush(p.Accent) };
            Canvas.SetLeft(dot, c - 2);
            Canvas.SetTop(dot, c - 2);
            cv.Children.Add(dot);

            card.Padding = new Thickness(4);
            card.Child = cv;
            card.MouseLeftButtonUp += delegate { openPop("calendar", card); };

            h.RefreshAction = delegate
            {
                DateTime now = DateTime.Now;
                double sAng = (now.Second * 6.0 - 90.0) * Math.PI / 180.0;
                double mAng = ((now.Minute + now.Second / 60.0) * 6.0 - 90.0) * Math.PI / 180.0;
                double hAng = (((now.Hour % 12) + now.Minute / 60.0) * 30.0 - 90.0) * Math.PI / 180.0;

                hrHand.X2 = c + Math.Cos(hAng) * (c * 0.48);
                hrHand.Y2 = c + Math.Sin(hAng) * (c * 0.48);
                minHand.X2 = c + Math.Cos(mAng) * (c * 0.72);
                minHand.Y2 = c + Math.Sin(mAng) * (c * 0.72);
                secHand.X2 = c + Math.Cos(sAng) * (c * 0.80);
                secHand.Y2 = c + Math.Sin(sAng) * (c * 0.80);
            };
        }

        // ====== 23. WORLD CLOCKS ======
        private static void BuildWorldClocks(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, double scale, WidgetCardHandle h)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            card.Child = row;

            h.RefreshAction = delegate
            {
                row.Children.Clear();
                DateTime utc = DateTime.UtcNow;
                int limit = Math.Min(3, cfg.WorldClockZones.Count);
                for (int i = 0; i < limit; i++)
                {
                    string[] parts = cfg.WorldClockZones[i].Split('|');
                    string name = parts[0];
                    int offset = 0;
                    if (parts.Length > 1) int.TryParse(parts[1], out offset);
                    DateTime local = utc.AddHours(offset);

                    StackPanel col = new StackPanel { Margin = new Thickness(i == 0 ? 0 : 7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                    col.Children.Add(new TextBlock
                    {
                        Text = name.Length > 3 ? name.Substring(0, 3).ToUpperInvariant() : name.ToUpperInvariant(),
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = Math.Round(8.2 * scale),
                        FontWeight = FontWeights.SemiBold,
                        Foreground = ThemeManager.Brush(p.TextMuted),
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                    col.Children.Add(new TextBlock
                    {
                        Text = local.ToString("HH:mm"),
                        FontFamily = new FontFamily("Segoe UI"),
                        FontSize = Math.Round(11.5 * scale),
                        FontWeight = FontWeights.Bold,
                        Foreground = ThemeManager.Brush(p.TextPrimary),
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                    row.Children.Add(col);
                }
            };
        }

        // ====== 24. GITHUB ACTIVITY HEATMAP ======
        private static void BuildGitHub(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            StackPanel meta = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0), MaxWidth = Math.Round(56 * scale) };
            TextBlock userTxt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(9.5 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(p.TextPrimary),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            TextBlock repoTxt = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(8.5 * scale),
                Foreground = ThemeManager.Brush(p.TextMuted)
            };
            meta.Children.Add(userTxt);
            meta.Children.Add(repoTxt);

            Grid heat = new Grid { VerticalAlignment = VerticalAlignment.Center };
            for (int r = 0; r < 4; r++) heat.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int c = 0; c < 7; c++) heat.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(meta);
            row.Children.Add(heat);
            card.Child = row;

            card.MouseLeftButtonUp += delegate
            {
                try { Process.Start(new ProcessStartInfo("https://github.com/" + cfg.GitHubUser) { UseShellExecute = true }); } catch { }
            };

            h.RefreshAction = delegate
            {
                userTxt.Text = "@" + tel.GitHub.Username;
                repoTxt.Text = tel.GitHub.Loaded ? (tel.GitHub.PublicRepos + " repos") : "GitHub";
                heat.Children.Clear();
                Color[] greens = new Color[]
                {
                    Color.FromArgb(40, p.IsLight ? (byte)0 : (byte)255, p.IsLight ? (byte)0 : (byte)255, p.IsLight ? (byte)0 : (byte)255),
                    Color.FromRgb(14, 68, 41),
                    Color.FromRgb(0, 109, 50),
                    Color.FromRgb(38, 166, 65),
                    Color.FromRgb(57, 211, 83)
                };
                double cell = Math.Max(4.5, Math.Round(5.8 * scale));
                for (int i = 0; i < 28; i++)
                {
                    int lvl = Math.Max(0, Math.Min(4, tel.GitHub.Heatmap28[i]));
                    Border b = new Border
                    {
                        Width = cell,
                        Height = cell,
                        CornerRadius = new CornerRadius(1.5),
                        Margin = new Thickness(1),
                        Background = ThemeManager.Brush(greens[lvl])
                    };
                    Grid.SetColumn(b, i / 4);
                    Grid.SetRow(b, i % 4);
                    heat.Children.Add(b);
                }
            };
        }

        // ====== 25. QUICK COMMANDS ======
        private static void BuildQuickCommands(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = "\u26A1",
                FontSize = Math.Round(15 * scale),
                Foreground = ThemeManager.Brush(p.Accent),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = L10n.T(cfg, "Komut", "Run"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(8.8 * scale),
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(p.TextSecondary),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            card.Child = sp;
            card.MouseLeftButtonUp += delegate { openPop("commands", card); };
        }

        // ====== 26. CLIPBOARD HISTORY ======
        private static void BuildClipboard(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, SystemTelemetry tel, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = Math.Round(108 * scale) };
            StackPanel top = new StackPanel { Orientation = Orientation.Horizontal };
            top.Children.Add(new TextBlock
            {
                Text = "\uE77F",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = Math.Round(9.5 * scale),
                Foreground = ThemeManager.Brush(Color.FromRgb(6, 182, 212)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            });
            TextBlock hdr = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(8.5 * scale),
                FontWeight = FontWeights.Bold,
                Foreground = ThemeManager.Brush(Color.FromRgb(6, 182, 212))
            };
            top.Children.Add(hdr);

            TextBlock snip = new TextBlock
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(10.2 * scale),
                Foreground = ThemeManager.Brush(p.TextPrimary),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0)
            };
            sp.Children.Add(top);
            sp.Children.Add(snip);
            card.Child = sp;
            card.MouseLeftButtonUp += delegate { openPop("clipboard", card); };

            h.RefreshAction = delegate
            {
                int cnt = tel.ClipboardHistory.Count;
                hdr.Text = L10n.T(cfg, "PANO", "CLIP") + (cnt > 0 ? (" (" + cnt + ")") : "");
                if (cnt > 0)
                {
                    string s = tel.ClipboardHistory[0].Replace("\r", " ").Replace("\n", " ");
                    snip.Text = s;
                }
                else
                {
                    snip.Text = L10n.T(cfg, "Kopyalanan yok", "Empty");
                }
            };
        }

        // ====== 27. WORKSPACES & WINDOW TILING ======
        private static void BuildWorkspaces(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            row.Children.Add(MakeToggleChip("\uE76B", p, scale, delegate { NativeWin32.SwitchVirtualDesktop(false); }));
            row.Children.Add(MakeToggleChip("\uE7C4", p, scale, delegate { openPop("workspaces", card); }));
            row.Children.Add(MakeToggleChip("\uE76C", p, scale, delegate { NativeWin32.SwitchVirtualDesktop(true); }));
            card.Padding = new Thickness(5, 3, 5, 3);
            card.Child = row;
        }

        // ====== 28. CONTROL CENTER TILE ======
        private static void BuildControlCenterTile(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = "\uE9E9",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = Math.Round(15 * scale),
                Foreground = ThemeManager.Brush(p.Accent),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = L10n.T(cfg, "Kontrol", "Control"),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(8.5 * scale),
                FontWeight = FontWeights.SemiBold,
                Foreground = ThemeManager.Brush(p.TextSecondary),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            card.Child = sp;
            card.MouseLeftButtonUp += delegate { openPop("control_center", card); };
        }

        // ====== 29. SPOTLIGHT SEARCH PILL ======
        private static void BuildSpotlightTile(Border card, MonolithConfig cfg, ThemePalette p, DockMetrics m, double scale, WidgetCardHandle h, Action<string, FrameworkElement> openPop)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            row.Children.Add(new TextBlock
            {
                Text = "\uE721",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = Math.Round(12.5 * scale),
                Foreground = ThemeManager.Brush(p.Accent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            row.Children.Add(new TextBlock
            {
                Text = L10n.T(cfg, "Ara...", "Search..."),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = Math.Round(11.5 * scale),
                FontWeight = FontWeights.Medium,
                Foreground = ThemeManager.Brush(p.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center
            });
            card.Child = row;
            card.MouseLeftButtonUp += delegate { openPop("spotlight", card); };
        }
    }
}
