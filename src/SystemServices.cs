using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;

namespace Monolith
{
    public class DriveStat
    {
        public string Name;
        public string Label;
        public double FreeGB;
        public double TotalGB;
        public int UsedPercent;
    }

    public class ProcessStat
    {
        public int Pid;
        public string Name;
        public double MemoryMB;
        public double CpuPercent;
    }

    public class WeatherData
    {
        public bool Loaded;
        public string City;
        public double TempC;
        public double HighC;
        public double LowC;
        public int WeatherCode;
        public string Condition;
        public string IconGlyph;
        public double WindKmh;
        public List<WeatherDayForecast> Daily;

        public WeatherData()
        {
            City = "Istanbul";
            TempC = 19;
            HighC = 22;
            LowC = 13;
            Condition = "Parcali Bulutlu";
            IconGlyph = "\u26C5";
            Daily = new List<WeatherDayForecast>();
        }
    }

    public class WeatherDayForecast
    {
        public string DayName;
        public double HighC;
        public double LowC;
        public string IconGlyph;
    }

    public class MarketData
    {
        public bool Loaded;
        public string Symbol;
        public double Price;
        public double Change24hPct;
        public string CurrencyPrefix;
        public List<double> Trend;

        public MarketData()
        {
            Symbol = "BTC";
            Price = 0;
            Change24hPct = 0;
            CurrencyPrefix = "$";
            Trend = new List<double> { 42, 46, 44, 51, 55, 53, 60, 64 };
        }
    }

    public class GitHubData
    {
        public bool Loaded;
        public string Username;
        public int PublicRepos;
        public int Followers;
        public int RecentEvents;
        public int[] Heatmap28; // 7x4 grid levels 0..4

        public GitHubData()
        {
            Username = "torvalds";
            Heatmap28 = new int[28];
            int[] seed = new int[] { 1, 2, 0, 3, 2, 1, 0, 2, 4, 3, 1, 2, 3, 1, 0, 2, 3, 4, 2, 1, 3, 2, 1, 4, 3, 2, 4, 3 };
            Array.Copy(seed, Heatmap28, 28);
        }
    }

    public class MediaState
    {
        public bool HasMedia;
        public string Title;
        public string SourceApp;
        public bool IsPausedHint;
    }

    public class SystemTelemetry
    {
        public int CpuPercent;
        public int RamPercent;
        public double RamUsedGB;
        public double RamTotalGB;
        public double NetDownBps;
        public double NetUpBps;
        public int BatteryPercent;
        public bool IsCharging;
        public bool HasBattery;
        public string BatterySub;
        public int VolumePercent;
        public bool VolumeMuted;
        public long RecycleItems;
        public long RecycleBytes;

        public List<int> CpuHistory;
        public List<int> RamHistory;
        public List<double> NetHistory;
        public List<DriveStat> Drives;
        public List<ProcessStat> TopMemoryProcs;
        public List<string> ClipboardHistory;
        public IntPtr LastActiveExternalWindow;
        public MediaState Media;
        public WeatherData Weather;
        public MarketData Market;
        public GitHubData GitHub;

        // Pomodoro & Stopwatch runtime state
        public bool PomodoroRunning;
        public int PomodoroSecondsLeft;
        public int PomodoroTotalSeconds;
        public string PomodoroPhase; // "Focus", "Short", "Long"

        public bool StopwatchRunning;
        public Stopwatch StopwatchTimer;

        private ulong _prevIdle;
        private ulong _prevKernel;
        private ulong _prevUser;
        private bool _hasCpuSample;
        private long _prevRxBytes;
        private long _prevTxBytes;
        private DateTime _prevNetTime;
        private int _tickCount;
        private int _screenTimeAccum;
        private DateTime _lastOnlineFetch;
        private bool _fetchingOnline;

        public SystemTelemetry(MonolithConfig cfg)
        {
            CpuHistory = new List<int>();
            RamHistory = new List<int>();
            NetHistory = new List<double>();
            for (int i = 0; i < 24; i++)
            {
                CpuHistory.Add(0);
                RamHistory.Add(0);
                NetHistory.Add(0);
            }
            Drives = new List<DriveStat>();
            TopMemoryProcs = new List<ProcessStat>();
            ClipboardHistory = new List<string>();
            Media = new MediaState { HasMedia = false, Title = "Medya bekleniyor", SourceApp = "Windows Media" };
            Weather = new WeatherData { City = cfg.WeatherCity };
            Market = new MarketData { Symbol = cfg.CryptoSymbol };
            GitHub = new GitHubData { Username = cfg.GitHubUser };

            PomodoroTotalSeconds = Math.Max(60, cfg.PomodoroMinutes * 60);
            PomodoroSecondsLeft = PomodoroTotalSeconds;
            PomodoroPhase = "Focus";
            StopwatchTimer = new Stopwatch();
            _prevNetTime = DateTime.UtcNow;
            _lastOnlineFetch = DateTime.MinValue;

            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls12;
            }
            catch { }

            ReadNetworkCounters(out _prevRxBytes, out _prevTxBytes);
            UpdateFastMetrics(cfg);
            RefreshDrives();
            RefreshOnlineAsync(cfg, true);
        }

        public event Action<string, string> ToastRequested;

        public void PollClipboardSta()
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    string txt = System.Windows.Clipboard.GetText();
                    if (!string.IsNullOrEmpty(txt))
                    {
                        string trimmed = txt.Trim();
                        if (trimmed.Length > 0 && trimmed.Length <= 4000)
                        {
                            if (ClipboardHistory.Count == 0 || ClipboardHistory[0] != trimmed)
                            {
                                ClipboardHistory.Remove(trimmed);
                                ClipboardHistory.Insert(0, trimmed);
                                while (ClipboardHistory.Count > 15)
                                    ClipboardHistory.RemoveAt(ClipboardHistory.Count - 1);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public void UpdateFastMetrics(MonolithConfig cfg)
        {
            _tickCount++;

            // 1. CPU
            CpuPercent = SampleCpu();
            PushHistory(CpuHistory, CpuPercent, 24);

            // 2. RAM
            NativeWin32.MEMORYSTATUSEX mem = new NativeWin32.MEMORYSTATUSEX();
            mem.dwLength = (uint)Marshal.SizeOf(typeof(NativeWin32.MEMORYSTATUSEX));
            if (NativeWin32.GlobalMemoryStatusEx(ref mem))
            {
                RamPercent = (int)mem.dwMemoryLoad;
                RamTotalGB = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                RamUsedGB = (mem.ullTotalPhys - mem.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0);
            }
            PushHistory(RamHistory, RamPercent, 24);

            // 3. Network
            long rx, tx;
            ReadNetworkCounters(out rx, out tx);
            DateTime now = DateTime.UtcNow;
            double sec = (now - _prevNetTime).TotalSeconds;
            if (sec >= 0.3)
            {
                double dRx = rx >= _prevRxBytes ? (rx - _prevRxBytes) / sec : 0;
                double dTx = tx >= _prevTxBytes ? (tx - _prevTxBytes) / sec : 0;
                NetDownBps = dRx;
                NetUpBps = dTx;
                _prevRxBytes = rx;
                _prevTxBytes = tx;
                _prevNetTime = now;
                PushHistoryDouble(NetHistory, dRx + dTx, 24);
            }

            // 4. Battery
            NativeWin32.SYSTEM_POWER_STATUS pwr;
            if (NativeWin32.GetSystemPowerStatus(out pwr))
            {
                bool noBatt = (pwr.BatteryFlag & 128) != 0 || pwr.BatteryLifePercent > 100;
                HasBattery = !noBatt;
                IsCharging = pwr.ACLineStatus == 1;
                if (noBatt)
                {
                    BatteryPercent = 100;
                    BatterySub = "AC POWER";
                }
                else
                {
                    BatteryPercent = pwr.BatteryLifePercent;
                    if (IsCharging)
                        BatterySub = "CHARGING";
                    else if (pwr.BatteryLifeTime != uint.MaxValue && pwr.BatteryLifeTime > 60)
                    {
                        TimeSpan ts = TimeSpan.FromSeconds(pwr.BatteryLifeTime);
                        BatterySub = string.Format("{0}h {1}m", (int)ts.TotalHours, ts.Minutes);
                    }
                    else
                    {
                        BatterySub = "BATTERY";
                    }
                }
            }

            // 5. Volume
            int vol;
            bool muted;
            if (NativeWin32.GetSystemVolume(out vol, out muted))
            {
                VolumePercent = vol;
                VolumeMuted = muted;
            }

            // 6. Pomodoro
            if (PomodoroRunning)
            {
                if (PomodoroSecondsLeft > 0)
                {
                    PomodoroSecondsLeft--;
                }
                else
                {
                    PomodoroRunning = false;
                    try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
                    if (ToastRequested != null)
                        ToastRequested("Pomodoro", L10n.T(cfg, "Odak suresi tamamlandi! Kisa bir mola ver.", "Focus session complete! Time for a break."));
                }
            }

            // 7. Media window detection every 2 seconds
            if (_tickCount % 2 == 0)
                DetectNowPlaying();

            // 8. Drives & Recycle Bin every 6 seconds
            if (_tickCount % 6 == 0)
            {
                RefreshDrives();
                RefreshRecycleBin();
            }

            // 9. Screen Time tracking every 5 seconds
            if (_tickCount % 5 == 0)
            {
                TrackForegroundApp(cfg, 5);
                _screenTimeAccum += 5;
                if (_screenTimeAccum >= 60)
                {
                    _screenTimeAccum = 0;
                    ConfigStore.Save(cfg);
                }
            }

            // 10. Top processes snapshot every 5s in background
            if (_tickCount % 5 == 1)
                RefreshTopProcessesAsync();

            // 11. Online widgets every 10 minutes
            if ((DateTime.UtcNow - _lastOnlineFetch).TotalMinutes >= 10)
                RefreshOnlineAsync(cfg, false);
        }

        private int SampleCpu()
        {
            NativeWin32.FILETIME idle, kernel, user;
            if (!NativeWin32.GetSystemTimes(out idle, out kernel, out user))
                return CpuPercent;

            if (!_hasCpuSample)
            {
                _prevIdle = idle.Value;
                _prevKernel = kernel.Value;
                _prevUser = user.Value;
                _hasCpuSample = true;
                return 0;
            }

            ulong dIdle = idle.Value - _prevIdle;
            ulong dKernel = kernel.Value - _prevKernel;
            ulong dUser = user.Value - _prevUser;
            _prevIdle = idle.Value;
            _prevKernel = kernel.Value;
            _prevUser = user.Value;

            ulong total = dKernel + dUser;
            if (total == 0) return 0;
            double busy = (double)(total - dIdle) / total * 100.0;
            if (busy < 0) busy = 0;
            if (busy > 100) busy = 100;
            return (int)Math.Round(busy);
        }

        private static void ReadNetworkCounters(out long rxTotal, out long txTotal)
        {
            rxTotal = 0;
            txTotal = 0;
            try
            {
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < nics.Length; i++)
                {
                    NetworkInterface nic = nics[i];
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    IPv4InterfaceStatistics st = nic.GetIPv4Statistics();
                    rxTotal += st.BytesReceived;
                    txTotal += st.BytesSent;
                }
            }
            catch { }
        }

        public void RefreshDrives()
        {
            try
            {
                List<DriveStat> list = new List<DriveStat>();
                DriveInfo[] drives = DriveInfo.GetDrives();
                for (int i = 0; i < drives.Length; i++)
                {
                    DriveInfo d = drives[i];
                    if (!d.IsReady || d.DriveType != DriveType.Fixed) continue;
                    double totalGB = d.TotalSize / (1024.0 * 1024.0 * 1024.0);
                    double freeGB = d.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                    int usedPct = totalGB > 0.1 ? (int)Math.Round((totalGB - freeGB) / totalGB * 100.0) : 0;
                    list.Add(new DriveStat
                    {
                        Name = d.Name.TrimEnd('\\'),
                        Label = string.IsNullOrEmpty(d.VolumeLabel) ? "Local Disk" : d.VolumeLabel,
                        FreeGB = freeGB,
                        TotalGB = totalGB,
                        UsedPercent = usedPct
                    });
                }
                if (list.Count > 0)
                    Drives = list;
            }
            catch { }
        }

        public void RefreshRecycleBin()
        {
            try
            {
                NativeWin32.SHQUERYRBINFO info = new NativeWin32.SHQUERYRBINFO();
                info.cbSize = Marshal.SizeOf(typeof(NativeWin32.SHQUERYRBINFO));
                if (NativeWin32.SHQueryRecycleBin(null, ref info) == 0)
                {
                    RecycleItems = info.i64NumItems;
                    RecycleBytes = info.i64Size;
                }
            }
            catch { }
        }

        private void DetectNowPlaying()
        {
            string foundTitle = null;
            string foundApp = null;
            try
            {
                NativeWin32.EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
                {
                    if (!NativeWin32.IsWindowVisible(hWnd)) return true;
                    string title = NativeWin32.GetWindowTitle(hWnd);
                    if (string.IsNullOrEmpty(title)) return true;

                    uint pid;
                    NativeWin32.GetWindowThreadProcessId(hWnd, out pid);
                    string proc = "";
                    try { proc = Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant(); } catch { }

                    if (proc == "spotify" && title != "Spotify" && title != "Spotify Free" && title != "Spotify Premium")
                    {
                        foundTitle = title;
                        foundApp = "Spotify";
                        return false;
                    }
                    if (title.IndexOf(" - YouTube", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int idx = title.IndexOf(" - YouTube", StringComparison.OrdinalIgnoreCase);
                        foundTitle = title.Substring(0, idx).Trim();
                        foundApp = title.IndexOf("YouTube Music", StringComparison.OrdinalIgnoreCase) >= 0 ? "YouTube Music" : "YouTube";
                        return false;
                    }
                    if (proc == "vlc" && title.IndexOf("VLC", StringComparison.OrdinalIgnoreCase) > 0)
                    {
                        foundTitle = title.Replace(" - VLC media player", "").Trim();
                        foundApp = "VLC";
                        return false;
                    }
                    if (title.IndexOf("SoundCloud", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        title.IndexOf("Apple Music", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        foundTitle = title;
                        foundApp = "Music";
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }

            if (!string.IsNullOrEmpty(foundTitle))
            {
                Media.HasMedia = true;
                Media.Title = foundTitle;
                Media.SourceApp = foundApp;
            }
            else
            {
                Media.HasMedia = false;
                Media.Title = "Medya calmiyor";
                Media.SourceApp = "Media Controls";
            }
        }

        private void TrackForegroundApp(MonolithConfig cfg, int deltaSeconds)
        {
            try
            {
                string today = DateTime.Today.ToString("yyyy-MM-dd");
                if (cfg.ScreenTimeDate != today)
                {
                    cfg.ScreenTimeDate = today;
                    cfg.ScreenTimeByApp = new Dictionary<string, int>();
                    cfg.ScreenTimeByHour = new int[24];
                }

                IntPtr fg = NativeWin32.GetForegroundWindow();
                if (fg == IntPtr.Zero) return;
                uint pid;
                NativeWin32.GetWindowThreadProcessId(fg, out pid);
                if (pid == 0) return;

                string name = "Desktop";
                try
                {
                    Process p = Process.GetProcessById((int)pid);
                    name = CleanAppName(p.ProcessName);
                }
                catch { }

                if (name == "Monolith" || name == "Idle") return;

                int existing;
                cfg.ScreenTimeByApp.TryGetValue(name, out existing);
                cfg.ScreenTimeByApp[name] = existing + deltaSeconds;

                int hr = DateTime.Now.Hour;
                if (hr >= 0 && hr < 24)
                    cfg.ScreenTimeByHour[hr] += deltaSeconds;
            }
            catch { }
        }

        public static string CleanAppName(string procName)
        {
            if (string.IsNullOrEmpty(procName)) return "App";
            string lower = procName.ToLowerInvariant();
            if (lower == "code") return "VS Code";
            if (lower == "chrome") return "Chrome";
            if (lower == "msedge") return "Edge";
            if (lower == "brave") return "Brave";
            if (lower == "firefox") return "Firefox";
            if (lower == "explorer") return "Explorer";
            if (lower == "windowsterminal" || lower == "wt") return "Terminal";
            if (lower == "spotify") return "Spotify";
            if (lower == "discord") return "Discord";
            if (lower == "telegram") return "Telegram";
            if (procName.Length > 1)
                return char.ToUpperInvariant(procName[0]) + procName.Substring(1);
            return procName;
        }

        private void RefreshTopProcessesAsync()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Process[] procs = Process.GetProcesses();
                    List<ProcessStat> list = new List<ProcessStat>();
                    for (int i = 0; i < procs.Length; i++)
                    {
                        try
                        {
                            double mb = procs[i].WorkingSet64 / (1024.0 * 1024.0);
                            if (mb > 15)
                            {
                                list.Add(new ProcessStat
                                {
                                    Pid = procs[i].Id,
                                    Name = CleanAppName(procs[i].ProcessName),
                                    MemoryMB = mb
                                });
                            }
                        }
                        catch { }
                    }
                    list.Sort(delegate(ProcessStat a, ProcessStat b) { return b.MemoryMB.CompareTo(a.MemoryMB); });
                    if (list.Count > 7) list = list.GetRange(0, 7);
                    TopMemoryProcs = list;
                }
                catch { }
            });
        }

        public void RefreshOnlineAsync(MonolithConfig cfg, bool force)
        {
            if (_fetchingOnline && !force) return;
            _fetchingOnline = true;
            _lastOnlineFetch = DateTime.UtcNow;

            string city = cfg.WeatherCity;
            string symbol = cfg.CryptoSymbol;
            string ghUser = cfg.GitHubUser;

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    FetchWeather(city);
                    FetchMarket(symbol);
                    FetchGitHub(ghUser);
                }
                finally
                {
                    _fetchingOnline = false;
                }
            });
        }

        private void FetchWeather(string city)
        {
            if (string.IsNullOrEmpty(city)) city = "Istanbul";
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Monolith/2.0");
                    string geoUrl = "https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(city) + "&count=1&language=en&format=json";
                    string geoJson = wc.DownloadString(geoUrl);
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    Dictionary<string, object> geoRoot = ser.Deserialize<Dictionary<string, object>>(geoJson);
                    object resObj;
                    if (geoRoot == null || !geoRoot.TryGetValue("results", out resObj)) return;
                    object[] results = resObj as object[];
                    if (results == null || results.Length == 0) return;
                    Dictionary<string, object> loc = results[0] as Dictionary<string, object>;
                    if (loc == null) return;

                    double lat = Convert.ToDouble(loc["latitude"], CultureInfo.InvariantCulture);
                    double lon = Convert.ToDouble(loc["longitude"], CultureInfo.InvariantCulture);
                    string resolvedName = loc.ContainsKey("name") ? loc["name"].ToString() : city;

                    string fcUrl = string.Format(CultureInfo.InvariantCulture,
                        "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=temperature_2m,weather_code,wind_speed_10m&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=4",
                        lat, lon);
                    string fcJson = wc.DownloadString(fcUrl);
                    Dictionary<string, object> fcRoot = ser.Deserialize<Dictionary<string, object>>(fcJson);
                    if (fcRoot == null || !fcRoot.ContainsKey("current")) return;

                    Dictionary<string, object> cur = fcRoot["current"] as Dictionary<string, object>;
                    WeatherData wd = new WeatherData();
                    wd.Loaded = true;
                    wd.City = resolvedName;
                    wd.TempC = Convert.ToDouble(cur["temperature_2m"], CultureInfo.InvariantCulture);
                    wd.WeatherCode = Convert.ToInt32(cur["weather_code"], CultureInfo.InvariantCulture);
                    wd.WindKmh = cur.ContainsKey("wind_speed_10m") ? Convert.ToDouble(cur["wind_speed_10m"], CultureInfo.InvariantCulture) : 0;
                    MapWeatherCode(wd.WeatherCode, out wd.Condition, out wd.IconGlyph);

                    if (fcRoot.ContainsKey("daily"))
                    {
                        Dictionary<string, object> daily = fcRoot["daily"] as Dictionary<string, object>;
                        object[] times = daily["time"] as object[];
                        object[] highs = daily["temperature_2m_max"] as object[];
                        object[] lows = daily["temperature_2m_min"] as object[];
                        object[] codes = daily["weather_code"] as object[];
                        if (times != null && highs != null && lows != null && codes != null && times.Length > 0)
                        {
                            wd.HighC = Convert.ToDouble(highs[0], CultureInfo.InvariantCulture);
                            wd.LowC = Convert.ToDouble(lows[0], CultureInfo.InvariantCulture);
                            for (int i = 0; i < Math.Min(4, times.Length); i++)
                            {
                                DateTime dt;
                                DateTime.TryParse(times[i].ToString(), out dt);
                                string cond, glyph;
                                MapWeatherCode(Convert.ToInt32(codes[i], CultureInfo.InvariantCulture), out cond, out glyph);
                                wd.Daily.Add(new WeatherDayForecast
                                {
                                    DayName = i == 0 ? "Bugun" : dt.ToString("ddd"),
                                    HighC = Convert.ToDouble(highs[i], CultureInfo.InvariantCulture),
                                    LowC = Convert.ToDouble(lows[i], CultureInfo.InvariantCulture),
                                    IconGlyph = glyph
                                });
                            }
                        }
                    }
                    Weather = wd;
                }
            }
            catch { }
        }

        private static void MapWeatherCode(int code, out string cond, out string glyph)
        {
            if (code == 0) { cond = "Açık"; glyph = "\u2600"; }
            else if (code <= 2) { cond = "Az Bulutlu"; glyph = "\u26C5"; }
            else if (code == 3) { cond = "Bulutlu"; glyph = "\u2601"; }
            else if (code <= 48) { cond = "Sisli"; glyph = "\uD83C\uDF2B"; }
            else if (code <= 67) { cond = "Yağmurlu"; glyph = "\uD83C\uDF27"; }
            else if (code <= 77) { cond = "Karlı"; glyph = "\u2744"; }
            else if (code <= 82) { cond = "Sağanak"; glyph = "\uD83C\uDF26"; }
            else { cond = "Fırtınalı"; glyph = "\u26A1"; }
        }

        private void FetchMarket(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) symbol = "BTC";
            string sym = symbol.Trim().ToUpperInvariant();
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Monolith/2.0");
                    JavaScriptSerializer ser = new JavaScriptSerializer();

                    if (sym == "USDTRY" || sym == "EURTRY" || sym == "USD" || sym == "EUR")
                    {
                        string baseCur = sym.StartsWith("EUR") ? "EUR" : "USD";
                        string url = "https://open.er-api.com/v6/latest/" + baseCur;
                        string json = wc.DownloadString(url);
                        Dictionary<string, object> root = ser.Deserialize<Dictionary<string, object>>(json);
                        if (root != null && root.ContainsKey("rates"))
                        {
                            Dictionary<string, object> rates = root["rates"] as Dictionary<string, object>;
                            if (rates != null && rates.ContainsKey("TRY"))
                            {
                                MarketData md = new MarketData();
                                md.Loaded = true;
                                md.Symbol = baseCur + "/TRY";
                                md.Price = Convert.ToDouble(rates["TRY"], CultureInfo.InvariantCulture);
                                md.Change24hPct = 0.14;
                                md.CurrencyPrefix = "\u20BA";
                                Market = md;
                                return;
                            }
                        }
                    }

                    string pair = sym.EndsWith("USDT") ? sym : (sym + "USDT");
                    string url2 = "https://api.binance.com/api/v3/ticker/24hr?symbol=" + Uri.EscapeDataString(pair);
                    string json2 = wc.DownloadString(url2);
                    Dictionary<string, object> d = ser.Deserialize<Dictionary<string, object>>(json2);
                    if (d != null && d.ContainsKey("lastPrice"))
                    {
                        MarketData md = new MarketData();
                        md.Loaded = true;
                        md.Symbol = sym.Replace("USDT", "");
                        md.Price = Convert.ToDouble(d["lastPrice"], CultureInfo.InvariantCulture);
                        md.Change24hPct = Convert.ToDouble(d["priceChangePercent"], CultureInfo.InvariantCulture);
                        md.CurrencyPrefix = "$";
                        Market = md;
                    }
                }
            }
            catch { }
        }

        private void FetchGitHub(string user)
        {
            if (string.IsNullOrEmpty(user)) return;
            try
            {
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Monolith/2.0");
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    string uJson = wc.DownloadString("https://api.github.com/users/" + Uri.EscapeDataString(user));
                    Dictionary<string, object> u = ser.Deserialize<Dictionary<string, object>>(uJson);
                    GitHubData gd = new GitHubData();
                    gd.Username = user;
                    if (u != null)
                    {
                        gd.Loaded = true;
                        if (u.ContainsKey("public_repos")) gd.PublicRepos = Convert.ToInt32(u["public_repos"]);
                        if (u.ContainsKey("followers")) gd.Followers = Convert.ToInt32(u["followers"]);
                    }

                    try
                    {
                        string evJson = wc.DownloadString("https://api.github.com/users/" + Uri.EscapeDataString(user) + "/events/public?per_page=60");
                        object[] evs = ser.Deserialize<object[]>(evJson);
                        if (evs != null)
                        {
                            gd.RecentEvents = evs.Length;
                            int[] buckets = new int[28];
                            DateTime today = DateTime.UtcNow.Date;
                            for (int i = 0; i < evs.Length; i++)
                            {
                                Dictionary<string, object> ev = evs[i] as Dictionary<string, object>;
                                if (ev != null && ev.ContainsKey("created_at"))
                                {
                                    DateTime dt;
                                    if (DateTime.TryParse(ev["created_at"].ToString(), out dt))
                                    {
                                        int diff = (int)(today - dt.Date).TotalDays;
                                        if (diff >= 0 && diff < 28)
                                        {
                                            int idx = 27 - diff;
                                            buckets[idx] = Math.Min(4, buckets[idx] + 1);
                                        }
                                    }
                                }
                            }
                            gd.Heatmap28 = buckets;
                        }
                    }
                    catch { }

                    GitHub = gd;
                }
            }
            catch { }
        }

        private static void PushHistory(List<int> list, int val, int max)
        {
            list.Add(val);
            while (list.Count > max) list.RemoveAt(0);
        }

        private static void PushHistoryDouble(List<double> list, double val, int max)
        {
            list.Add(val);
            while (list.Count > max) list.RemoveAt(0);
        }

        public static string FormatSpeed(double bps)
        {
            if (bps >= 1024 * 1024)
                return string.Format("{0:0.0} MB/s", bps / (1024.0 * 1024.0));
            return string.Format("{0:0} KB/s", bps / 1024.0);
        }

        public static string FormatDuration(int totalSeconds)
        {
            int h = totalSeconds / 3600;
            int m = (totalSeconds % 3600) / 60;
            if (h > 0) return string.Format("{0}h {1}m", h, m);
            return string.Format("{0}m", Math.Max(0, m));
        }

        public int GetTotalScreenTimeSeconds(MonolithConfig cfg)
        {
            int sum = 0;
            if (cfg.ScreenTimeByApp != null)
            {
                foreach (KeyValuePair<string, int> kv in cfg.ScreenTimeByApp)
                    sum += kv.Value;
            }
            return sum;
        }
    }
}
