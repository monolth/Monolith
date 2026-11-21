using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Monolith
{
    internal static class NativeWin32
    {
        public const int SW_HIDE = 0;
        public const int SW_SHOWNORMAL = 1;
        public const int SW_SHOW = 5;
        public const int SW_MINIMIZE = 6;
        public const int SW_SHOWNA = 8;
        public const int SW_RESTORE = 9;

        public const uint GW_OWNER = 4;
        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;
        public const int WS_CAPTION = 0x00C00000;
        public const int WS_THICKFRAME = 0x00040000;
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_APPWINDOW = 0x00040000;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const uint LWA_ALPHA = 0x00000002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_FRAMECHANGED = 0x0020;

        public const int WM_CLOSE = 0x0010;
        public const int WM_SYSCOMMAND = 0x0112;
        public const int SC_CLOSE = 0xF060;
        public const int WM_GETICON = 0x007F;
        public const int WM_HOTKEY = 0x0312;
        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;
        public const int ICON_SMALL2 = 2;
        public const int GCL_HICON = -14;
        public const int GCL_HICONSM = -34;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_NOREPEAT = 0x4000;

        public const uint VK_LWIN = 0x5B;
        public const uint VK_F11 = 0x7A;
        public const uint VK_W = 0x57;
        public const uint VK_S = 0x53;
        public const uint VK_SPACE_KEY = 0x20;
        public const byte VK_B = 0x42;
        public const byte VK_N = 0x4E;
        public const byte VK_A = 0x41;
        public const byte VK_D = 0x44;
        public const byte VK_P = 0x50;
        public const byte VK_R = 0x52;
        public const byte VK_TAB = 0x09;
        public const byte VK_CONTROL = 0x11;
        public const byte VK_LEFT = 0x25;
        public const byte VK_RIGHT = 0x27;
        public const byte VK_SPACE = 0x20;
        public const byte VK_RETURN = 0x0D;
        public const byte VK_ESCAPE = 0x1B;
        public const int SW_MAXIMIZE = 3;

        public const int DWM_TNP_RECTDESTINATION = 0x00000001;
        public const int DWM_TNP_OPACITY = 0x00000004;
        public const int DWM_TNP_VISIBLE = 0x00000008;
        public const int DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

        public const byte VK_VOLUME_MUTE = 0xAD;
        public const byte VK_VOLUME_DOWN = 0xAE;
        public const byte VK_VOLUME_UP = 0xAF;
        public const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        public const byte VK_MEDIA_PREV_TRACK = 0xB1;
        public const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        public const uint KEYEVENTF_KEYUP = 0x0002;

        public const uint SPI_GETDESKWALLPAPER = 0x0073;
        public const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
        public const int QUNS_PRESENTATION_MODE = 4;

        public const uint ABM_NEW = 0x00000000;
        public const uint ABM_REMOVE = 0x00000001;
        public const uint ABM_QUERYPOS = 0x00000002;
        public const uint ABM_SETPOS = 0x00000003;
        public const uint ABM_GETSTATE = 0x00000004;
        public const uint ABM_SETSTATE = 0x0000000A;
        public const uint ABS_AUTOHIDE = 0x00000001;
        public const uint ABE_TOP = 1;
        public const uint ABE_BOTTOM = 3;
        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_LARGEICON = 0x000000000;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        public const int DWMWA_CLOAKED = 14;
        public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public uint Low;
            public uint High;
            public ulong Value
            {
                get { return ((ulong)High << 32) | Low; }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PSIZE
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DWM_THUMBNAIL_PROPERTIES
        {
            public int dwFlags;
            public RECT rcDestination;
            public RECT rcSource;
            public byte opacity;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fVisible;
            [MarshalAs(UnmanagedType.Bool)]
            public bool fSourceClientAreaOnly;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("shell32.dll")]
        public static extern int SHQueryUserNotificationState(out int pquns);

        [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out uint pcrKey, out byte pbAlpha, out uint pdwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtr")]
        private static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetClassLong")]
        private static extern IntPtr GetClassLong32(IntPtr hWnd, int nIndex);

        public static IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8) return GetClassLongPtr64(hWnd, nIndex);
            return GetClassLong32(hWnd, nIndex);
        }

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        public static extern bool LockWorkStation();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, StringBuilder pvParam, uint fWinIni);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        public static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);

        [DllImport("dwmapi.dll")]
        public static extern int DwmUnregisterThumbnail(IntPtr thumb);

        [DllImport("dwmapi.dll")]
        public static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out PSIZE size);

        [DllImport("dwmapi.dll")]
        public static extern int DwmUpdateThumbnailProperties(IntPtr hThumbnail, ref DWM_THUMBNAIL_PROPERTIES props);

        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        public static string GetWallpaperPath()
        {
            try
            {
                StringBuilder sb = new StringBuilder(512);
                if (SystemParametersInfo(SPI_GETDESKWALLPAPER, (uint)sb.Capacity, sb, 0))
                {
                    string path = sb.ToString();
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        return path;
                }
            }
            catch { }

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                {
                    if (key != null)
                    {
                        string p = key.GetValue("WallPaper") as string;
                        if (!string.IsNullOrEmpty(p) && File.Exists(p))
                            return p;
                    }
                }
            }
            catch { }
            return null;
        }

        public static System.Windows.Media.Color GetDesktopFallbackColor()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors"))
                {
                    if (key != null)
                    {
                        string val = key.GetValue("Background") as string;
                        if (!string.IsNullOrEmpty(val))
                        {
                            string[] parts = val.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 3)
                            {
                                byte r, g, b;
                                if (byte.TryParse(parts[0], out r) && byte.TryParse(parts[1], out g) && byte.TryParse(parts[2], out b))
                                    return System.Windows.Media.Color.FromRgb(r, g, b);
                            }
                        }
                    }
                }
            }
            catch { }
            return System.Windows.Media.Color.FromRgb(22, 34, 58);
        }

        public static bool IsWindowCloaked(IntPtr hwnd)
        {
            try
            {
                int cloaked = 0;
                int hr = DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out cloaked, Marshal.SizeOf(typeof(int)));
                return hr == 0 && cloaked != 0;
            }
            catch
            {
                return false;
            }
        }

        public static string GetWindowClassName(IntPtr hwnd)
        {
            StringBuilder sb = new StringBuilder(128);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string GetWindowTitle(IntPtr hwnd)
        {
            int len = GetWindowTextLength(hwnd);
            if (len <= 0) return string.Empty;
            StringBuilder sb = new StringBuilder(len + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static void SendKeyPress(byte vk)
        {
            keybd_event(vk, 0, 0, UIntPtr.Zero);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void OpenStartMenu()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(25);
                SendKeyPress((byte)VK_LWIN);
            });
        }

        public static void OpenWindowsSearch()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(65);
                SendWinCombo((byte)VK_S);
            });
        }

        public static void OpenProjectDisplay()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(35);
                SendWinCombo(VK_P);
            });
        }

        public static void OpenWindowsQuickSettings()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(35);
                SendWinCombo(VK_A);
            });
        }

        public static void OpenNotifications()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(35);
                SendWinCombo(VK_N);
            });
        }

        public static void OpenRunDialog()
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(35);
                SendWinCombo(VK_R);
            });
        }

        /// <summary>
        /// Immediately requests a window to close asynchronously via WM_SYSCOMMAND(SC_CLOSE) + WM_CLOSE
        /// so the dock UI thread never stalls.
        /// </summary>
        public static void CloseWindowGracefullyAsync(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (!IsWindow(hwnd)) return;
                    // SC_CLOSE is the exact message Windows sends when clicking the native titlebar X
                    PostMessage(hwnd, WM_SYSCOMMAND, (IntPtr)SC_CLOSE, IntPtr.Zero);
                    Thread.Sleep(45);
                    if (IsWindow(hwnd) && IsWindowVisible(hwnd))
                    {
                        PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    }
                }
                catch { }
            });
        }

        public static void SendWinCombo(byte vk)
        {
            keybd_event((byte)VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event(vk, 0, 0, UIntPtr.Zero);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void OpenTaskView()
        {
            SendWinCombo(VK_TAB);
        }

        public static void ShowDesktop()
        {
            SendWinCombo(VK_D);
        }

        public static void SwitchVirtualDesktop(bool next)
        {
            byte dir = next ? VK_RIGHT : VK_LEFT;
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event((byte)VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event(dir, 0, 0, UIntPtr.Zero);
            keybd_event(dir, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void CreateVirtualDesktop()
        {
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event((byte)VK_LWIN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_D, 0, 0, UIntPtr.Zero);
            keybd_event(VK_D, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)VK_LWIN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static void PutComputerToSleep()
        {
            try { SetSuspendState(false, false, false); } catch { }
        }

        /// <summary>
        /// 1-click active window tiling/snapping (Left, Right, Center, Maximize).
        /// </summary>
        public static void SnapWindow(IntPtr hwnd, string mode)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return;
            try
            {
                if (mode == "max")
                {
                    ShowWindow(hwnd, SW_MAXIMIZE);
                    SetForegroundWindow(hwnd);
                    return;
                }

                ShowWindow(hwnd, SW_RESTORE);
                int sw = GetSystemMetrics(0);
                int sh = GetSystemMetrics(1);
                int margin = 10;
                int dockReserve = 78;
                int usableH = Math.Max(300, sh - dockReserve - margin);

                int x = margin, y = margin, w = sw - margin * 2, h = usableH;
                if (mode == "left")
                {
                    w = (sw - margin * 3) / 2;
                    x = margin;
                }
                else if (mode == "right")
                {
                    w = (sw - margin * 3) / 2;
                    x = margin * 2 + w;
                }
                else if (mode == "center")
                {
                    w = (int)Math.Round(sw * 0.72);
                    h = (int)Math.Round(usableH * 0.86);
                    x = (sw - w) / 2;
                    y = (usableH - h) / 2 + margin;
                }

                SetWindowPos(hwnd, IntPtr.Zero, x, y, w, h, SWP_NOZORDER);
                SetForegroundWindow(hwnd);
            }
            catch { }
        }

        public static bool TryGetScreenBrightness(out int percent)
        {
            percent = 80;
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("root\\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness"))
                using (ManagementObjectCollection col = s.Get())
                {
                    foreach (ManagementObject obj in col)
                    {
                        percent = Convert.ToInt32(obj["CurrentBrightness"]);
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public static void SetScreenBrightness(int percent)
        {
            if (percent < 5) percent = 5;
            if (percent > 100) percent = 100;
            try
            {
                using (ManagementObjectSearcher s = new ManagementObjectSearcher("root\\WMI", "SELECT * FROM WmiMonitorBrightnessMethods"))
                using (ManagementObjectCollection col = s.Get())
                {
                    foreach (ManagementObject obj in col)
                    {
                        obj.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)percent });
                        return;
                    }
                }
            }
            catch { }
        }

        private static bool IsOverflowWindowActuallyOpen(IntPtr h)
        {
            if (h == IntPtr.Zero || !IsWindowVisible(h)) return false;
            if (IsWindowCloaked(h)) return false;
            RECT rc;
            if (!GetWindowRect(h, out rc)) return false;
            int w = rc.Right - rc.Left;
            int hgt = rc.Bottom - rc.Top;
            return w >= 32 && hgt >= 32;
        }

        public static IntPtr FindHiddenIconsOverflowWindow()
        {
            // Windows 11 XAML Island overflow window (must be visible, uncloaked, and actually sized)
            IntPtr h11 = FindWindow("TopLevelWindowForOverflowXamlIsland", null);
            if (IsOverflowWindowActuallyOpen(h11))
                return h11;

            // Windows 10 / legacy overflow window
            IntPtr h10 = FindWindow("NotifyIconOverflowWindow", null);
            if (IsOverflowWindowActuallyOpen(h10))
                return h10;

            return IntPtr.Zero;
        }

        public static bool IsHiddenIconsFlyoutOpen()
        {
            return FindHiddenIconsOverflowWindow() != IntPtr.Zero;
        }

        public static bool IsCursorOverNativeTaskbar()
        {
            try
            {
                IntPtr tray = FindWindow("Shell_TrayWnd", null);
                if (tray == IntPtr.Zero || !IsWindowVisible(tray)) return false;
                RECT rc;
                POINT pt;
                if (GetWindowRect(tray, out rc) && GetCursorPos(out pt))
                {
                    return pt.X >= rc.Left && pt.X <= rc.Right && pt.Y >= rc.Top && pt.Y <= rc.Bottom;
                }
            }
            catch { }
            return false;
        }

        public static bool RepositionHiddenIconsFlyout(int anchorCenterX, int dockTopPx, int dockBottomPx, bool isTop)
        {
            IntPtr h = FindHiddenIconsOverflowWindow();
            if (h == IntPtr.Zero) return false;
            return MoveOverflowToAnchor(h, anchorCenterX, dockTopPx, dockBottomPx, isTop);
        }

        private static bool MoveOverflowToAnchor(IntPtr h, int anchorCenterX, int dockTopPx, int dockBottomPx, bool isTop)
        {
            try
            {
                RECT winRc;
                if (!GetWindowRect(h, out winRc)) return false;
                int w = winRc.Right - winRc.Left;
                int hgt = winRc.Bottom - winRc.Top;
                if (w <= 4 || hgt <= 4) return false;

                // Account for any invisible DWM shadow margin around Win11 XAML flyout
                int shadowTop = 0;
                int shadowBottom = 0;
                RECT visRc;
                if (DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out visRc, Marshal.SizeOf(typeof(RECT))) == 0)
                {
                    int vw = visRc.Right - visRc.Left;
                    int vh = visRc.Bottom - visRc.Top;
                    if (vw > 4 && vh > 4 && vw <= w && vh <= hgt)
                    {
                        shadowTop = Math.Max(0, visRc.Top - winRc.Top);
                        shadowBottom = Math.Max(0, winRc.Bottom - visRc.Bottom);
                    }
                }

                int screenW = GetSystemMetrics(0);
                int screenH = GetSystemMetrics(1);
                int gap = 10;

                int targetX = anchorCenterX - (w / 2);
                if (targetX < 8) targetX = 8;
                if (targetX + w > screenW - 8) targetX = screenW - w - 8;

                int targetY;
                if (isTop)
                    targetY = dockBottomPx + gap - shadowTop;
                else
                    targetY = dockTopPx - hgt - gap + shadowBottom;

                if (targetY < 6) targetY = 6;
                if (targetY + hgt > screenH - 6) targetY = screenH - hgt - 6;

                if (Math.Abs(winRc.Left - targetX) > 2 || Math.Abs(winRc.Top - targetY) > 2)
                {
                    SetWindowPos(h, HWND_TOPMOST, targetX, targetY, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Hides phantom Windows 11/10 taskbar focus tooltips (e.g. "Gizli simgeleri göster" / "Show hidden icons")
        /// spawned by Explorer near the bottom-right corner when Win+B wakes the hidden taskbar.
        /// Never touches the real overflow flyout or real app context menus.
        /// </summary>
        public static void SuppressExplorerTaskbarTooltips(IntPtr trayHwnd, bool strictEdgeOnly)
        {
            try
            {
                if (trayHwnd == IntPtr.Zero) trayHwnd = FindWindow("Shell_TrayWnd", null);
                if (trayHwnd == IntPtr.Zero) return;

                uint trayPid;
                GetWindowThreadProcessId(trayHwnd, out trayPid);
                if (trayPid == 0) return;

                int sw = GetSystemMetrics(0);
                int sh = GetSystemMetrics(1);
                IntPtr overflow = FindHiddenIconsOverflowWindow();
                RECT ovRc = new RECT();
                bool hasOv = (overflow != IntPtr.Zero) && GetWindowRect(overflow, out ovRc);

                EnumWindows(delegate(IntPtr h, IntPtr lParam)
                {
                    if (h == trayHwnd || h == overflow || !IsWindowVisible(h)) return true;

                    uint pid;
                    GetWindowThreadProcessId(h, out pid);
                    if (pid != trayPid) return true;

                    string cls = GetWindowClassName(h);
                    if (cls == "TopLevelWindowForOverflowXamlIsland" || cls == "NotifyIconOverflowWindow" ||
                        cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                        return true;

                    bool isKnownTooltip = (cls == "Xaml_WindowedPopupClass" || cls == "tooltips_class32" || cls == "PopupHost");
                    bool isTrayOwnedPopup = (GetWindow(h, GW_OWNER) == trayHwnd);
                    if (!isKnownTooltip && !isTrayOwnedPopup)
                        return true;

                    RECT rc;
                    if (!GetWindowRect(h, out rc)) return true;
                    int w = rc.Right - rc.Left;
                    int hgt = rc.Bottom - rc.Top;
                    if (w <= 0 || w > 420 || hgt <= 0 || hgt > 68) return true;

                    bool nearTaskbarEdge = (rc.Bottom >= sh - 150) || (rc.Top <= 150);
                    if (!nearTaskbarEdge) return true;

                    if (strictEdgeOnly && hasOv)
                    {
                        bool overlapsOverflow = rc.Right > ovRc.Left - 12 && rc.Left < ovRc.Right + 12 &&
                                                rc.Bottom > ovRc.Top - 48 && rc.Top < ovRc.Bottom + 12;
                        // Only suppress late tooltips outside our anchored overflow menu (e.g. bottom-right old tray)
                        if (overlapsOverflow && rc.Right < sw - 80) return true;
                    }

                    ShowWindowAsync(h, SW_HIDE);
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
        }

        private static void GhostSleep(IntPtr tray, int totalMs)
        {
            int elapsed = 0;
            while (elapsed < totalMs)
            {
                if (tray != IntPtr.Zero)
                {
                    GhostTaskbar(tray);
                    SuppressExplorerTaskbarTooltips(tray, false);
                }
                int step = Math.Min(15, totalMs - elapsed);
                Thread.Sleep(step);
                elapsed += step;
            }
            if (tray != IntPtr.Zero)
            {
                GhostTaskbar(tray);
                SuppressExplorerTaskbarTooltips(tray, false);
            }
        }

        /// <summary>
        /// Opens or dismisses the native Windows 11/10 "Show Hidden Icons" system tray overflow flyout,
        /// keeps Shell_TrayWnd 100% invisible (layered alpha = 0 + click-through) even when ABS_AUTOHIDE is active,
        /// and anchors the Hidden Icons flyout directly above (or below) Monolith's own '^' button!
        /// </summary>
        public static void TriggerHiddenIconsFlyoutAsync(int anchorCenterX, int dockTopPx, int dockBottomPx, bool isTop, Action<bool> onCompleted)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                bool opened = false;
                try
                {
                    // 1. If already genuinely open (visible + uncloaked + sized), dismiss cleanly
                    IntPtr existing = FindHiddenIconsOverflowWindow();
                    if (existing != IntPtr.Zero)
                    {
                        SendKeyPress(VK_ESCAPE);
                        if (onCompleted != null) onCompleted(false);
                        return;
                    }

                    // 2. Make Shell_TrayWnd 100% transparent & click-through BEFORE waking it up!
                    IntPtr tray = FindWindow("Shell_TrayWnd", null);
                    if (tray != IntPtr.Zero)
                    {
                        GhostTaskbar(tray);
                        if (!IsWindowVisible(tray))
                        {
                            ShowWindow(tray, SW_SHOWNA);
                            GhostSleep(tray, 45);
                        }
                    }

                    // 3. Wake up auto-hidden Windows 11 taskbar via Win+B (stays 100% alpha=0 invisible!)
                    //    Short 110ms wait wakes the tray before Explorer's focus tooltip settles; tooltips suppressed every 15ms!
                    SendWinCombo(VK_B);
                    GhostSleep(tray, 110);

                    // 4. Stage A: Invoke the real chevron button via UIAutomation on the now-awake taskbar
                    bool uiaInvoked = false;
                    if (tray != IntPtr.Zero)
                    {
                        try
                        {
                            AutomationElement root = AutomationElement.FromHandle(tray);
                            if (root != null)
                            {
                                AutomationElement btn = FindTrayChevronButton(root);
                                if (btn != null)
                                {
                                    object patObj;
                                    if (btn.TryGetCurrentPattern(InvokePattern.Pattern, out patObj))
                                    {
                                        ((InvokePattern)patObj).Invoke();
                                        uiaInvoked = true;
                                    }
                                    else if (btn.TryGetCurrentPattern(TogglePattern.Pattern, out patObj))
                                    {
                                        ((TogglePattern)patObj).Toggle();
                                        uiaInvoked = true;
                                    }
                                }
                            }
                        }
                        catch { }
                    }

                    if (tray != IntPtr.Zero) GhostTaskbar(tray);

                    if (uiaInvoked)
                    {
                        opened = WaitAndAnchorOverflow(tray, anchorCenterX, dockTopPx, dockBottomPx, isTop, 420);
                    }

                    // 5. Stage B: Press Space (Win+B already placed keyboard focus right on the '^' chevron)
                    if (!opened)
                    {
                        if (tray != IntPtr.Zero) GhostTaskbar(tray);
                        SendKeyPress(VK_SPACE);
                        opened = WaitAndAnchorOverflow(tray, anchorCenterX, dockTopPx, dockBottomPx, isTop, 360);
                    }

                    // 6. Stage C: Re-focus with Win+B -> Enter (handles slower Win11 auto-hide wakeups)
                    if (!opened)
                    {
                        SendWinCombo(VK_B);
                        GhostSleep(tray, 260);
                        SendKeyPress(VK_RETURN);
                        opened = WaitAndAnchorOverflow(tray, anchorCenterX, dockTopPx, dockBottomPx, isTop, 360);
                    }

                    // 7. Stage D: Legacy Windows 10 NotifyIconOverflowWindow direct fallback (never Win11 XAML host)
                    if (!opened)
                    {
                        IntPtr h10 = FindWindow("NotifyIconOverflowWindow", null);
                        if (h10 != IntPtr.Zero)
                        {
                            MoveOverflowToAnchor(h10, anchorCenterX, dockTopPx, dockBottomPx, isTop);
                            ShowWindowAsync(h10, SW_SHOW);
                            SetForegroundWindow(h10);
                            if (tray != IntPtr.Zero) GhostTaskbar(tray);
                            opened = WaitAndAnchorOverflow(tray, anchorCenterX, dockTopPx, dockBottomPx, isTop, 220);
                        }
                    }

                    // 8. Re-anchor smoothly while Windows' flyout entrance animation settles & suppress focus tooltips
                    if (opened)
                    {
                        for (int k = 0; k < 10; k++)
                        {
                            GhostSleep(tray, 40);
                            RepositionHiddenIconsFlyout(anchorCenterX, dockTopPx, dockBottomPx, isTop);
                        }
                    }
                    else
                    {
                        SuppressExplorerTaskbarTooltips(tray, false);
                    }
                }
                catch { }

                if (onCompleted != null) onCompleted(opened);
            });
        }

        private static bool WaitAndAnchorOverflow(IntPtr tray, int anchorCenterX, int dockTopPx, int dockBottomPx, bool isTop, int maxWaitMs)
        {
            int elapsed = 0;
            while (elapsed < maxWaitMs)
            {
                if (tray != IntPtr.Zero)
                {
                    GhostTaskbar(tray);
                    SuppressExplorerTaskbarTooltips(tray, false);
                }
                IntPtr h = FindHiddenIconsOverflowWindow();
                if (h != IntPtr.Zero)
                {
                    MoveOverflowToAnchor(h, anchorCenterX, dockTopPx, dockBottomPx, isTop);
                    return true;
                }
                Thread.Sleep(15);
                elapsed += 15;
            }
            return false;
        }

        private static AutomationElement FindTrayChevronButton(AutomationElement root)
        {
            // 1. Windows 10 Notification Chevron AutomationId ("1502")
            try
            {
                AutomationElement win10 = root.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, "1502"));
                if (win10 != null) return win10;
            }
            catch { }

            try
            {
                AutomationElementCollection buttons = root.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                if (buttons != null)
                {
                    AutomationElement fallbackSystemTray = null;
                    for (int i = 0; i < buttons.Count; i++)
                    {
                        AutomationElement el = buttons[i];
                        string aid = "";
                        string cls = "";
                        string name = "";
                        try { aid = el.Current.AutomationId ?? ""; } catch { }
                        try { cls = el.Current.ClassName ?? ""; } catch { }
                        try { name = el.Current.Name ?? ""; } catch { }

                        string lowerName = name.ToLower(System.Globalization.CultureInfo.CurrentCulture);
                        string lowerInv = name.ToLowerInvariant();
                        string lowerCls = cls.ToLowerInvariant();
                        if (lowerCls.IndexOf("chevron", StringComparison.Ordinal) >= 0 ||
                            lowerName.IndexOf("gizli", StringComparison.Ordinal) >= 0 ||
                            lowerInv.IndexOf("hidden", StringComparison.Ordinal) >= 0 ||
                            lowerInv.IndexOf("chevron", StringComparison.Ordinal) >= 0 ||
                            lowerInv.IndexOf("overflow", StringComparison.Ordinal) >= 0 ||
                            lowerName.IndexOf("taşma", StringComparison.Ordinal) >= 0 ||
                            lowerName.IndexOf("bildirim köşesi", StringComparison.Ordinal) >= 0)
                        {
                            return el;
                        }

                        if (aid == "SystemTrayIcon" && fallbackSystemTray == null)
                            fallbackSystemTray = el;
                    }
                    return fallbackSystemTray;
                }
            }
            catch { }
            return null;
        }

        public static ImageSource ExtractIconFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try
            {
                if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                string resolved = Environment.ExpandEnvironmentVariables(path);
                if (!File.Exists(resolved) && !Directory.Exists(resolved))
                {
                    string sys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), resolved);
                    if (File.Exists(sys)) resolved = sys;
                    else
                    {
                        string sys32 = Path.Combine(Environment.SystemDirectory, resolved);
                        if (File.Exists(sys32)) resolved = sys32;
                    }
                }

                SHFILEINFO shinfo = new SHFILEINFO();
                uint flags = SHGFI_ICON | SHGFI_LARGEICON;
                if (!File.Exists(resolved) && !Directory.Exists(resolved))
                    flags |= SHGFI_USEFILEATTRIBUTES;

                IntPtr res = SHGetFileInfo(resolved, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), flags);
                if (shinfo.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        BitmapSource src = Imaging.CreateBitmapSourceFromHIcon(
                            shinfo.hIcon,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromWidthAndHeight(48, 48));
                        src.Freeze();
                        return src;
                    }
                    finally
                    {
                        DestroyIcon(shinfo.hIcon);
                    }
                }
            }
            catch { }
            return null;
        }

        public static ImageSource ExtractIconFromWindow(IntPtr hwnd, string processPath)
        {
            try
            {
                IntPtr hIcon = IntPtr.Zero;
                IntPtr res;
                SendMessageTimeout(hwnd, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero, 2, 120, out res);
                hIcon = res;
                if (hIcon == IntPtr.Zero)
                {
                    SendMessageTimeout(hwnd, WM_GETICON, (IntPtr)ICON_SMALL2, IntPtr.Zero, 2, 120, out res);
                    hIcon = res;
                }
                if (hIcon == IntPtr.Zero)
                {
                    SendMessageTimeout(hwnd, WM_GETICON, (IntPtr)ICON_SMALL, IntPtr.Zero, 2, 120, out res);
                    hIcon = res;
                }
                if (hIcon == IntPtr.Zero)
                    hIcon = GetClassLongPtr(hwnd, GCL_HICON);
                if (hIcon == IntPtr.Zero)
                    hIcon = GetClassLongPtr(hwnd, GCL_HICONSM);

                if (hIcon != IntPtr.Zero)
                {
                    BitmapSource src = Imaging.CreateBitmapSourceFromHIcon(
                        hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(48, 48));
                    src.Freeze();
                    return src;
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(processPath))
                return ExtractIconFromPath(processPath);

            return null;
        }

        public static void SetStartupEnabled(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        string exe = Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue("Monolith", "\"" + exe + "\"");
                    }
                    else
                    {
                        key.DeleteValue("Monolith", false);
                    }
                }
            }
            catch { }
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (key == null) return false;
                    object val = key.GetValue("Monolith");
                    return val != null && !string.IsNullOrEmpty(val.ToString());
                }
            }
            catch { return false; }
        }

        // ====== Taskbar & Native AppBar Management (Zero-Bounce, Crash-Safe & Always Restorable) ======
        private static readonly Dictionary<IntPtr, bool> HiddenTaskbars = new Dictionary<IntPtr, bool>();
        private static readonly Dictionary<IntPtr, int> OriginalTaskbarExStyles = new Dictionary<IntPtr, int>();
        private static bool _savedNativeAutoHideValid = false;
        private static uint _savedNativeAutoHideState = 0;
        private static bool _nativeAutoHideApplied = false;
        private static IntPtr _lastAutoHideTrayHwnd = IntPtr.Zero;

        private static HwndSource _monolithAppBarHost = null;
        private static bool _monolithAppBarRegistered = false;
        private static string _lastAppBarSig = "";

        /// <summary>
        /// Sets the native Windows taskbar to ABS_AUTOHIDE cleanly (once per Shell_TrayWnd handle!) so Explorer itself
        /// releases the 48px work area strip with ZERO bouncing or SPI_SETWORKAREA tug-of-war.
        /// Always restored on exit.
        /// </summary>
        public static void EnableNativeTaskbarAutoHideOnce()
        {
            try
            {
                IntPtr tray = FindWindow("Shell_TrayWnd", null);
                if (tray == IntPtr.Zero) return;
                if (_nativeAutoHideApplied && _lastAutoHideTrayHwnd == tray) return;

                APPBARDATA abd = new APPBARDATA();
                abd.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
                abd.hWnd = tray;

                if (!_savedNativeAutoHideValid)
                {
                    _savedNativeAutoHideState = (uint)SHAppBarMessage(ABM_GETSTATE, ref abd).ToInt64();
                    _savedNativeAutoHideValid = true;
                }

                uint curState = (uint)SHAppBarMessage(ABM_GETSTATE, ref abd).ToInt64();
                if ((curState & ABS_AUTOHIDE) == 0)
                {
                    abd.lParam = (IntPtr)(curState | ABS_AUTOHIDE);
                    SHAppBarMessage(ABM_SETSTATE, ref abd);
                }
                _nativeAutoHideApplied = true;
                _lastAutoHideTrayHwnd = tray;
            }
            catch { }
        }

        public static void RestoreNativeTaskbarAutoHide()
        {
            _nativeAutoHideApplied = false;
            _lastAutoHideTrayHwnd = IntPtr.Zero;
            if (!_savedNativeAutoHideValid) return;
            try
            {
                IntPtr tray = FindWindow("Shell_TrayWnd", null);
                if (tray != IntPtr.Zero)
                {
                    APPBARDATA abd = new APPBARDATA();
                    abd.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
                    abd.hWnd = tray;
                    abd.lParam = (IntPtr)_savedNativeAutoHideState;
                    SHAppBarMessage(ABM_SETSTATE, ref abd);
                }
                _savedNativeAutoHideValid = false;
            }
            catch { }
        }

        /// <summary>
        /// Registers or updates Monolith's own native Windows AppBar reservation (only in ReserveSpace / FullWidth mode)
        /// using an invisible helper HWND so Windows Explorer coordinates work area cleanly with zero bouncing.
        /// </summary>
        public static void ConfigureMonolithAppBar(bool reserveSpace, bool isTop, int reserveHeightPx)
        {
            try
            {
                if (!reserveSpace || reserveHeightPx <= 0)
                {
                    RemoveMonolithAppBar();
                    return;
                }

                int sw = GetSystemMetrics(0);
                int sh = GetSystemMetrics(1);
                if (sw < 200 || sh < 200 || reserveHeightPx >= sh / 3) return;

                string sig = (isTop ? "T" : "B") + "|" + reserveHeightPx + "|" + sw + "x" + sh;
                if (_monolithAppBarRegistered && sig == _lastAppBarSig)
                    return;

                if (_monolithAppBarHost == null)
                {
                    HwndSourceParameters p = new HwndSourceParameters("MonolithAppBarReserve");
                    p.Width = sw;
                    p.Height = reserveHeightPx;
                    p.PositionX = 0;
                    p.PositionY = isTop ? 0 : (sh - reserveHeightPx);
                    p.WindowStyle = unchecked((int)0x80000000); // WS_POPUP (hidden, never shown)
                    p.ExtendedWindowStyle = WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT;
                    _monolithAppBarHost = new HwndSource(p);
                }

                IntPtr hwnd = _monolithAppBarHost.Handle;
                if (hwnd == IntPtr.Zero) return;

                APPBARDATA abd = new APPBARDATA();
                abd.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
                abd.hWnd = hwnd;
                abd.uCallbackMessage = 0x0400 + 77; // WM_USER + 77

                if (!_monolithAppBarRegistered)
                {
                    SHAppBarMessage(ABM_NEW, ref abd);
                    _monolithAppBarRegistered = true;
                }

                abd.uEdge = isTop ? ABE_TOP : ABE_BOTTOM;
                abd.rc = new RECT
                {
                    Left = 0,
                    Right = sw,
                    Top = isTop ? 0 : (sh - reserveHeightPx),
                    Bottom = isTop ? reserveHeightPx : sh
                };

                SHAppBarMessage(ABM_QUERYPOS, ref abd);
                if (isTop) abd.rc.Bottom = abd.rc.Top + reserveHeightPx;
                else abd.rc.Top = abd.rc.Bottom - reserveHeightPx;

                SHAppBarMessage(ABM_SETPOS, ref abd);
                SetWindowPos(hwnd, IntPtr.Zero, abd.rc.Left, abd.rc.Top,
                    Math.Max(1, abd.rc.Right - abd.rc.Left),
                    Math.Max(1, abd.rc.Bottom - abd.rc.Top),
                    SWP_NOZORDER | SWP_NOACTIVATE);
                _lastAppBarSig = sig;
            }
            catch { }
        }

        public static void RemoveMonolithAppBar()
        {
            try
            {
                if (_monolithAppBarRegistered && _monolithAppBarHost != null && _monolithAppBarHost.Handle != IntPtr.Zero)
                {
                    APPBARDATA abd = new APPBARDATA();
                    abd.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
                    abd.hWnd = _monolithAppBarHost.Handle;
                    SHAppBarMessage(ABM_REMOVE, ref abd);
                }
            }
            catch { }
            _monolithAppBarRegistered = false;
            _lastAppBarSig = "";
            if (_monolithAppBarHost != null)
            {
                try { _monolithAppBarHost.Dispose(); } catch { }
                _monolithAppBarHost = null;
            }
        }

        public static void GhostTaskbar(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return;
            try
            {
                int ex = GetWindowLong(hWnd, GWL_EXSTYLE);
                if (!OriginalTaskbarExStyles.ContainsKey(hWnd))
                    OriginalTaskbarExStyles[hWnd] = ex;

                int ghostEx = ex | WS_EX_LAYERED | WS_EX_TRANSPARENT;
                if (ghostEx != ex)
                {
                    SetWindowLong(hWnd, GWL_EXSTYLE, ghostEx);
                    SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                }

                uint ck, fl;
                byte al;
                if (!GetLayeredWindowAttributes(hWnd, out ck, out al, out fl) || (fl & LWA_ALPHA) == 0 || al != 0)
                {
                    SetLayeredWindowAttributes(hWnd, 0, 0, LWA_ALPHA);
                }
            }
            catch { }
        }

        public static void KeepTaskbarGhostedIfVisible()
        {
            try
            {
                IntPtr tray = FindWindow("Shell_TrayWnd", null);
                if (tray != IntPtr.Zero)
                    GhostTaskbar(tray);
            }
            catch { }
        }

        private static void UnghostTaskbar(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return;
            try
            {
                SetLayeredWindowAttributes(hWnd, 0, 255, LWA_ALPHA);
                int origEx;
                if (OriginalTaskbarExStyles.TryGetValue(hWnd, out origEx))
                {
                    SetWindowLong(hWnd, GWL_EXSTYLE, origEx);
                    OriginalTaskbarExStyles.Remove(hWnd);
                }
                else
                {
                    int ex = GetWindowLong(hWnd, GWL_EXSTYLE);
                    SetWindowLong(hWnd, GWL_EXSTYLE, ex & ~WS_EX_TRANSPARENT);
                }
                SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            }
            catch { }
        }

        public static void HideNativeTaskbars()
        {
            try
            {
                EnableNativeTaskbarAutoHideOnce();
                EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
                {
                    string cls = GetWindowClassName(hWnd);
                    if (cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                    {
                        if (!HiddenTaskbars.ContainsKey(hWnd))
                            HiddenTaskbars[hWnd] = IsWindowVisible(hWnd);
                        // Keep Shell_TrayWnd WS_VISIBLE + alpha=0 + click-through (NEVER SW_HIDE!)
                        // Hiding Shell_TrayWnd with SW_HIDE breaks Windows 11 SearchHost (Win+S) and Start Menu Search!
                        GhostTaskbar(hWnd);
                        if (!IsWindowVisible(hWnd))
                        {
                            ShowWindow(hWnd, SW_SHOWNA);
                            GhostTaskbar(hWnd);
                        }
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
        }

        public static void RestoreNativeTaskbars()
        {
            try
            {
                RemoveMonolithAppBar();

                foreach (KeyValuePair<IntPtr, bool> kv in HiddenTaskbars)
                {
                    if (IsWindow(kv.Key))
                    {
                        UnghostTaskbar(kv.Key);
                        ShowWindow(kv.Key, SW_SHOW);
                    }
                }
                HiddenTaskbars.Clear();

                // Safety sweep in case explorer restarted
                EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
                {
                    string cls = GetWindowClassName(hWnd);
                    if (cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                    {
                        UnghostTaskbar(hWnd);
                        ShowWindow(hWnd, SW_SHOW);
                    }
                    return true;
                }, IntPtr.Zero);

                RestoreNativeTaskbarAutoHide();
            }
            catch { }
        }

        // ====== Core Audio COM Interop for Live System Master Volume ======
        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject { }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int NotImpl1();
            [PreserveSig]
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IntPtr pNotify);
            int UnregisterControlChangeNotify(IntPtr pNotify);
            int GetChannelCount(out int pnChannelCount);
            int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
            int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
            int GetMasterVolumeLevel(out float pfLevelDB);
            int GetMasterVolumeLevelScalar(out float pfLevel);
            int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
            int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
            int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        }

        private static IAudioEndpointVolume GetMasterVolumeEndpoint()
        {
            try
            {
                IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                IMMDevice device;
                // eRender = 0, eMultimedia = 1
                if (enumerator.GetDefaultAudioEndpoint(0, 1, out device) != 0 || device == null)
                    return null;
                Guid iid = typeof(IAudioEndpointVolume).GUID;
                object o;
                if (device.Activate(ref iid, 23, IntPtr.Zero, out o) != 0)
                    return null;
                return o as IAudioEndpointVolume;
            }
            catch
            {
                return null;
            }
        }

        public static bool GetSystemVolume(out int percent, out bool muted)
        {
            percent = 50;
            muted = false;
            try
            {
                IAudioEndpointVolume ep = GetMasterVolumeEndpoint();
                if (ep == null) return false;
                float scalar;
                ep.GetMasterVolumeLevelScalar(out scalar);
                bool m;
                ep.GetMute(out m);
                percent = (int)Math.Round(scalar * 100f);
                muted = m;
                Marshal.ReleaseComObject(ep);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void SetSystemVolume(int percent)
        {
            try
            {
                if (percent < 0) percent = 0;
                if (percent > 100) percent = 100;
                IAudioEndpointVolume ep = GetMasterVolumeEndpoint();
                if (ep == null) return;
                Guid g = Guid.Empty;
                ep.SetMasterVolumeLevelScalar(percent / 100f, ref g);
                Marshal.ReleaseComObject(ep);
            }
            catch { }
        }

        public static void ToggleSystemMute()
        {
            try
            {
                IAudioEndpointVolume ep = GetMasterVolumeEndpoint();
                if (ep == null)
                {
                    SendKeyPress(VK_VOLUME_MUTE);
                    return;
                }
                bool m;
                ep.GetMute(out m);
                Guid g = Guid.Empty;
                ep.SetMute(!m, ref g);
                Marshal.ReleaseComObject(ep);
            }
            catch
            {
                SendKeyPress(VK_VOLUME_MUTE);
            }
        }
    }
}
