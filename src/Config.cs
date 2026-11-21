using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace Monolith
{
    public class PinnedAppItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string IconPath { get; set; }
        public string Glyph { get; set; }
    }

    public class TodoEntry
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public bool Done { get; set; }
    }

    public class AiQuotaItem
    {
        public string Provider { get; set; }
        public string Plan { get; set; }
        public int PercentUsed { get; set; }
        public string ResetLabel { get; set; }
        public string AccentHex { get; set; }
    }

    public class QuickCommandItem
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public string Args { get; set; }
    }

    public class MonolithConfig
    {
        public string Theme { get; set; }
        public string ClearCardStyle { get; set; }
        public string Accent { get; set; }
        public string CustomAccentHex { get; set; }
        public string DockTintColor { get; set; } // "Natural", "Accent", preset name, or "#RRGGBB"
        public double ColorTintStrength { get; set; }
        public string IconStyle { get; set; } // 18 icon frame styles
        public string DockMediaMode { get; set; } // "None", "PresetAurora", "PresetCyber", "PresetSunset", "CustomFile"
        public string DockMediaPath { get; set; }
        public double DockMediaOpacity { get; set; }
        public string DockMediaFit { get; set; } // "Crop", "Tile", "Contain"
        public double DockMediaOffsetX { get; set; } // 0.0 (left) .. 1.0 (right)
        public double DockMediaOffsetY { get; set; } // 0.0 (top) .. 1.0 (bottom)
        public double DockMediaZoom { get; set; } // 1.0 .. 3.5
        public string SearchMode { get; set; } // "Integrated" (Win11-style above dock), "WindowsNative" (Win+S)
        public string CcStyle { get; set; } // "Windows11", "Compact", "GlassCards"
        public string CcCornerStyle { get; set; } // "Rounded", "Sharp", "Pill"
        public string CcSize { get; set; } // "S", "M", "L"
        public int CcColumns { get; set; } // 2 or 3
        public bool CcShowBrightness { get; set; }
        public bool CcShowVolume { get; set; }
        public bool CcShowMedia { get; set; }
        public bool CcShowPowerRow { get; set; }
        public List<string> CcActiveTiles { get; set; }
        public string DockSize { get; set; } // "S", "M", "L"
        public bool IsTop { get; set; }
        public bool FullWidth { get; set; }
        public string FullWidthAlign { get; set; } // "Center" (Windows 11) or "Left" (Windows 10)
        public string DockOverlapMode { get; set; } // "SmartHide" (Intellihide), "ReserveSpace", "FloatOver"
        public bool AutoHide { get; set; }
        public bool HideOnFullscreen { get; set; }
        public bool FastStartupMode { get; set; }
        public bool AntiCheatSafeMode { get; set; }
        public bool LowResourceMode { get; set; }
        public bool NamesOnHover { get; set; }
        public bool Use24HourClock { get; set; }
        public bool HideWindowsTaskbar { get; set; }
        public bool ShowOpenWindows { get; set; }
        public bool DockMagnification { get; set; }
        public bool LiveWindowPreviews { get; set; }
        public bool ShowSpotlightButton { get; set; }
        public bool ShowControlCenterButton { get; set; }
        public double GlassIntensity { get; set; }
        public string Language { get; set; } // "tr", "en"

        // Widget settings
        public string WeatherCity { get; set; }
        public string GitHubUser { get; set; }
        public string CryptoSymbol { get; set; }
        public int PomodoroMinutes { get; set; }
        public string NotesText { get; set; }
        public List<string> WorldClockZones { get; set; }

        public List<string> ActiveWidgets { get; set; }
        public List<PinnedAppItem> PinnedApps { get; set; }
        public List<TodoEntry> Todos { get; set; }
        public List<AiQuotaItem> AiQuotas { get; set; }
        public List<QuickCommandItem> QuickCommands { get; set; }

        // Screen time saved per day (yyyy-MM-dd -> appName -> seconds)
        public string ScreenTimeDate { get; set; }
        public Dictionary<string, int> ScreenTimeByApp { get; set; }
        public int[] ScreenTimeByHour { get; set; }

        public MonolithConfig()
        {
            Theme = "Spatial Glass";
            ClearCardStyle = "Glass";
            Accent = "Blue";
            CustomAccentHex = "";
            DockTintColor = "Natural";
            ColorTintStrength = 0.45;
            IconStyle = "Squircle";
            DockMediaMode = "None";
            DockMediaPath = "";
            DockMediaOpacity = 0.82;
            DockMediaFit = "Crop";
            DockMediaOffsetX = 0.5;
            DockMediaOffsetY = 0.5;
            DockMediaZoom = 1.0;
            SearchMode = "Integrated";
            CcStyle = "Windows11";
            CcCornerStyle = "Rounded";
            CcSize = "M";
            CcColumns = 3;
            CcShowBrightness = true;
            CcShowVolume = true;
            CcShowMedia = true;
            CcShowPowerRow = true;
            CcActiveTiles = new List<string> { "wifi", "bluetooth", "sound_output", "display", "nightlight", "focus" };
            DockSize = "M";
            IsTop = false;
            FullWidth = false;
            FullWidthAlign = "Center";
            DockOverlapMode = "SmartHide";
            AutoHide = false;
            HideOnFullscreen = true;
            FastStartupMode = true;
            AntiCheatSafeMode = true;
            LowResourceMode = false;
            NamesOnHover = true;
            Use24HourClock = true;
            HideWindowsTaskbar = true;
            ShowOpenWindows = true;
            DockMagnification = true;
            LiveWindowPreviews = true;
            ShowSpotlightButton = true;
            ShowControlCenterButton = true;
            GlassIntensity = 0.88;
            Language = "tr";

            WeatherCity = "Istanbul";
            GitHubUser = "torvalds";
            CryptoSymbol = "BTC";
            PomodoroMinutes = 25;
            NotesText = "Monolith hızlı notlar...\n• Bugünkü görevleri kontrol et\n• Yeni widget eklemek için sağ tık → Tüm Widget'lar";
            WorldClockZones = new List<string> { "Istanbul|3", "London|0", "New York|-5", "Tokyo|9" };

            ActiveWidgets = new List<string>
            {
                "ai_limits",
                "now_playing",
                "system_chart",
                "battery",
                "weather",
                "today"
            };

            PinnedApps = BuildDefaultPinnedApps();

            Todos = new List<TodoEntry>
            {
                new TodoEntry { Id = "1", Text = "Monolith widget düzenini özelleştir", Done = false },
                new TodoEntry { Id = "2", Text = "Günlük odak seansını başlat", Done = false },
                new TodoEntry { Id = "3", Text = "Masaüstü görünümünü kontrol et", Done = true }
            };

            AiQuotas = new List<AiQuotaItem>
            {
                new AiQuotaItem { Provider = "Claude", Plan = "Oturum", PercentUsed = 34, ResetLabel = "3s 20d", AccentHex = "#D97757" },
                new AiQuotaItem { Provider = "ChatGPT", Plan = "5s Limit", PercentUsed = 58, ResetLabel = "2s 40d", AccentHex = "#10A37F" },
                new AiQuotaItem { Provider = "Gemini", Plan = "Pro", PercentUsed = 22, ResetLabel = "18s", AccentHex = "#4285F4" }
            };

            QuickCommands = new List<QuickCommandItem>
            {
                new QuickCommandItem { Name = "Görev Yöneticisi", Command = "taskmgr.exe", Args = "" },
                new QuickCommandItem { Name = "Windows Ayarları", Command = "ms-settings:", Args = "" },
                new QuickCommandItem { Name = "Ses Ayarları", Command = "ms-settings:sound", Args = "" },
                new QuickCommandItem { Name = "Ekran Alıntısı", Command = "ms-screenclip:", Args = "" }
            };

            ScreenTimeDate = DateTime.Today.ToString("yyyy-MM-dd");
            ScreenTimeByApp = new Dictionary<string, int>();
            ScreenTimeByHour = new int[24];
        }

        public static List<PinnedAppItem> BuildDefaultPinnedApps()
        {
            List<PinnedAppItem> list = new List<PinnedAppItem>();

            string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            list.Add(new PinnedAppItem { Name = "Dosya Gezgini", Path = explorer, IconPath = explorer, Glyph = "\uE8B7" });

            string browser = FindFirstExisting(new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"BraveSoftware\Brave-Browser\Application\brave.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"BraveSoftware\Brave-Browser\Application\brave.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe")
            });
            if (!string.IsNullOrEmpty(browser))
                list.Add(new PinnedAppItem { Name = Path.GetFileNameWithoutExtension(browser).ToUpperInvariant() == "MSEDGE" ? "Microsoft Edge" : "Tarayıcı", Path = browser, IconPath = browser, Glyph = "\uE774" });
            else
                list.Add(new PinnedAppItem { Name = "Tarayıcı", Path = "https://www.google.com", IconPath = "", Glyph = "\uE774" });

            string vscode = FindFirstExisting(new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Microsoft VS Code\Code.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft VS Code\Code.exe")
            });
            if (!string.IsNullOrEmpty(vscode))
                list.Add(new PinnedAppItem { Name = "VS Code", Path = vscode, IconPath = vscode, Glyph = "\uE943" });

            string terminal = FindFirstExisting(new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\wt.exe"),
                Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
                Path.Combine(Environment.SystemDirectory, "cmd.exe")
            });
            if (!string.IsNullOrEmpty(terminal))
                list.Add(new PinnedAppItem { Name = "Terminal", Path = terminal, IconPath = terminal, Glyph = "\uE756" });

            string notepad = Path.Combine(Environment.SystemDirectory, "notepad.exe");
            if (File.Exists(notepad))
                list.Add(new PinnedAppItem { Name = "Not Defteri", Path = notepad, IconPath = notepad, Glyph = "\uE70B" });

            return list;
        }

        private static string FindFirstExisting(string[] paths)
        {
            foreach (string p in paths)
            {
                try
                {
                    if (!string.IsNullOrEmpty(p) && File.Exists(p))
                        return p;
                }
                catch { }
            }
            return null;
        }
    }

    public static class ConfigStore
    {
        public static string ConfigDir
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Monolith");
            }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(ConfigDir, "config.json"); }
        }

        public static MonolithConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    MonolithConfig cfg = ser.Deserialize<MonolithConfig>(json);
                    if (cfg != null)
                    {
                        EnsureDefaults(cfg);
                        return cfg;
                    }
                }
            }
            catch { }

            MonolithConfig fresh = new MonolithConfig();
            Save(fresh);
            return fresh;
        }

        private static void EnsureDefaults(MonolithConfig cfg)
        {
            if (string.IsNullOrEmpty(cfg.Theme)) cfg.Theme = "Spatial Glass";
            else if (cfg.Theme == "White") cfg.Theme = "Ceramic White";
            else if (cfg.Theme == "Black") cfg.Theme = "Obsidian Neon";
            else if (cfg.Theme.EndsWith("Frost", StringComparison.OrdinalIgnoreCase) && cfg.Theme != "Glacier Frost") cfg.Theme = "Glacier Frost";
            else if (!Array.Exists(ThemeManager.ThemeNames, delegate(string t) { return t == cfg.Theme; })) cfg.Theme = "Prism Glass";
            if (string.IsNullOrEmpty(cfg.ClearCardStyle)) cfg.ClearCardStyle = "Glass";
            if (string.IsNullOrEmpty(cfg.Accent)) cfg.Accent = "Blue";
            if (string.IsNullOrEmpty(cfg.DockTintColor)) cfg.DockTintColor = "Natural";
            if (cfg.ColorTintStrength < 0.05 || cfg.ColorTintStrength > 1.0) cfg.ColorTintStrength = 0.45;
            if (string.IsNullOrEmpty(cfg.IconStyle)) cfg.IconStyle = "Squircle";
            if (string.IsNullOrEmpty(cfg.DockMediaMode)) cfg.DockMediaMode = "None";
            if (cfg.DockMediaOpacity < 0.10 || cfg.DockMediaOpacity > 1.0) cfg.DockMediaOpacity = 0.82;
            if (string.IsNullOrEmpty(cfg.DockMediaFit) || cfg.DockMediaFit == "Cover" || cfg.DockMediaFit == "Stretch") cfg.DockMediaFit = "Crop";
            if (cfg.DockMediaOffsetX < 0.0 || cfg.DockMediaOffsetX > 1.0) cfg.DockMediaOffsetX = 0.5;
            if (cfg.DockMediaOffsetY < 0.0 || cfg.DockMediaOffsetY > 1.0) cfg.DockMediaOffsetY = 0.5;
            if (cfg.DockMediaZoom < 1.0 || cfg.DockMediaZoom > 3.5) cfg.DockMediaZoom = 1.0;
            if (string.IsNullOrEmpty(cfg.SearchMode) || cfg.SearchMode == "Windows" || cfg.SearchMode == "Spotlight") cfg.SearchMode = "Integrated";
            if (string.IsNullOrEmpty(cfg.CcStyle)) cfg.CcStyle = "Windows11";
            if (string.IsNullOrEmpty(cfg.CcCornerStyle)) cfg.CcCornerStyle = "Rounded";
            if (string.IsNullOrEmpty(cfg.CcSize)) cfg.CcSize = "M";
            if (cfg.CcColumns < 2 || cfg.CcColumns > 3) cfg.CcColumns = 3;
            if (cfg.CcActiveTiles == null || cfg.CcActiveTiles.Count == 0)
            {
                cfg.CcActiveTiles = new List<string> { "wifi", "bluetooth", "sound_output", "display", "nightlight", "focus" };
                cfg.CcShowBrightness = true;
                cfg.CcShowVolume = true;
                cfg.CcShowMedia = true;
                cfg.CcShowPowerRow = true;
            }
            else if (cfg.CcActiveTiles.Count == 6 &&
                     cfg.CcActiveTiles.Contains("wifi") &&
                     cfg.CcActiveTiles.Contains("bluetooth") &&
                     cfg.CcActiveTiles.Contains("display") &&
                     cfg.CcActiveTiles.Contains("snip") &&
                     !cfg.CcActiveTiles.Contains("sound_output"))
            {
                // Migrate legacy default 6-tile set so all 4 split-button tiles (wifi, bluetooth, sound_output, display) appear on main grid
                int snipIdx = cfg.CcActiveTiles.IndexOf("snip");
                if (snipIdx >= 0) cfg.CcActiveTiles[snipIdx] = "sound_output";
            }
            if (string.IsNullOrEmpty(cfg.DockSize)) cfg.DockSize = "M";
            if (string.IsNullOrEmpty(cfg.FullWidthAlign)) cfg.FullWidthAlign = "Center";
            if (string.IsNullOrEmpty(cfg.DockOverlapMode)) cfg.DockOverlapMode = "SmartHide";
            if (cfg.GlassIntensity < 0.2 || cfg.GlassIntensity > 1.0) cfg.GlassIntensity = 0.85;
            if (string.IsNullOrEmpty(cfg.Language)) cfg.Language = "tr";
            if (string.IsNullOrEmpty(cfg.WeatherCity)) cfg.WeatherCity = "Istanbul";
            if (string.IsNullOrEmpty(cfg.GitHubUser)) cfg.GitHubUser = "torvalds";
            if (string.IsNullOrEmpty(cfg.CryptoSymbol)) cfg.CryptoSymbol = "BTC";
            if (cfg.PomodoroMinutes < 1 || cfg.PomodoroMinutes > 120) cfg.PomodoroMinutes = 25;
            if (cfg.ActiveWidgets == null) cfg.ActiveWidgets = new List<string> { "ai_limits", "now_playing", "system_chart", "storage", "battery", "weather", "today" };
            if (cfg.PinnedApps == null || cfg.PinnedApps.Count == 0) cfg.PinnedApps = MonolithConfig.BuildDefaultPinnedApps();
            if (cfg.Todos == null) cfg.Todos = new List<TodoEntry>();
            if (cfg.AiQuotas == null || cfg.AiQuotas.Count == 0) cfg.AiQuotas = new MonolithConfig().AiQuotas;
            if (cfg.QuickCommands == null || cfg.QuickCommands.Count == 0) cfg.QuickCommands = new MonolithConfig().QuickCommands;
            if (cfg.WorldClockZones == null || cfg.WorldClockZones.Count == 0) cfg.WorldClockZones = new List<string> { "Istanbul|3", "London|0", "New York|-5" };

            string today = DateTime.Today.ToString("yyyy-MM-dd");
            if (cfg.ScreenTimeDate != today || cfg.ScreenTimeByApp == null || cfg.ScreenTimeByHour == null || cfg.ScreenTimeByHour.Length != 24)
            {
                cfg.ScreenTimeDate = today;
                cfg.ScreenTimeByApp = new Dictionary<string, int>();
                cfg.ScreenTimeByHour = new int[24];
            }
        }

        public static void Save(MonolithConfig cfg)
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                string json = ser.Serialize(cfg);
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }

    public static class L10n
    {
        public static bool IsTr(MonolithConfig cfg)
        {
            return cfg == null || string.Equals(cfg.Language, "tr", StringComparison.OrdinalIgnoreCase);
        }

        public static string T(MonolithConfig cfg, string tr, string en)
        {
            return IsTr(cfg) ? tr : en;
        }
    }
}
