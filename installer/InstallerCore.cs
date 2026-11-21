using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace MonolithSetup
{
    public class InstallOptions
    {
        public string InstallDir;
        public bool CreateDesktopShortcut;
        public bool CreateStartMenuShortcut;
        public bool LaunchAtStartup;
        public bool LaunchAfterInstall;

        public InstallOptions()
        {
            InstallDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Programs\Monolith");
            CreateDesktopShortcut = true;
            CreateStartMenuShortcut = true;
            LaunchAtStartup = true;
            LaunchAfterInstall = true;
        }
    }

    internal static class InstallerCore
    {
        public const string AppVersion = "1.0.4";
        private const string UninstallRegPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Monolith";
        private const string RunRegPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int cmd);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int idx);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int idx, int val);

        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        public static string GetInstalledLocation()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(UninstallRegPath, false))
                {
                    if (key != null)
                    {
                        string loc = key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(loc)) return loc;
                    }
                }
            }
            catch { }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Programs\Monolith");
        }

        public static bool IsAlreadyInstalled()
        {
            string dir = GetInstalledLocation();
            return File.Exists(Path.Combine(dir, "Monolith.exe"));
        }

        public static void RestoreWindowsTaskbar()
        {
            try
            {
                EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
                {
                    StringBuilder sb = new StringBuilder(64);
                    GetClassName(hWnd, sb, sb.Capacity);
                    string cls = sb.ToString();
                    if (cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd")
                    {
                        SetLayeredWindowAttributes(hWnd, 0, 255, 0x00000002);
                        int ex = GetWindowLong(hWnd, -20);
                        SetWindowLong(hWnd, -20, ex & ~0x00000020);
                        ShowWindow(hWnd, 5);
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
        }

        public static void StopRunningMonolith()
        {
            int selfPid = Process.GetCurrentProcess().Id;
            try
            {
                Process[] procs = Process.GetProcessesByName("Monolith");
                for (int i = 0; i < procs.Length; i++)
                {
                    Process p = procs[i];
                    if (p.Id == selfPid) continue;
                    try
                    {
                        p.CloseMainWindow();
                        if (!p.WaitForExit(1400))
                            p.Kill();
                        p.WaitForExit(1500);
                    }
                    catch { }
                }
            }
            catch { }
            RestoreWindowsTaskbar();
            Thread.Sleep(250);
        }

        public static void Install(InstallOptions opts, Action<int, string> onProgress)
        {
            if (onProgress != null) onProgress(10, "Çalışan Monolith örnekleri güvenli şekilde kapatılıyor...");
            StopRunningMonolith();

            if (onProgress != null) onProgress(25, "Kurulum klasörü hazırlanıyor...");
            Directory.CreateDirectory(opts.InstallDir);

            string targetExe = Path.Combine(opts.InstallDir, "Monolith.exe");
            string targetIco = Path.Combine(opts.InstallDir, "monolith.ico");
            string targetUninstaller = Path.Combine(opts.InstallDir, "MonolithUninstall.exe");

            if (onProgress != null) onProgress(48, "Monolith v" + AppVersion + " dosyaları çıkarılıyor...");
            ExtractGzipResource("MonolithPayload.exe.gz", targetExe);
            ExtractRawResource("MonolithIcon.ico", targetIco);

            if (onProgress != null) onProgress(65, "Kaldırma aracı (Uninstaller) yapılandırılıyor...");
            string selfExe = Process.GetCurrentProcess().MainModule.FileName;
            if (!string.Equals(Path.GetFullPath(selfExe), Path.GetFullPath(targetUninstaller), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(selfExe, targetUninstaller, true);
            }

            if (onProgress != null) onProgress(78, "Masaüstü ve Başlat Menüsü kısayolları oluşturuluyor...");
            if (opts.CreateDesktopShortcut)
            {
                string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Monolith.lnk");
                CreateShortcut(desktopLnk, targetExe, "", opts.InstallDir, targetIco, "Monolith - Spatial Glass Dock & Widget Bar");
            }
            if (opts.CreateStartMenuShortcut)
            {
                string startFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Monolith");
                Directory.CreateDirectory(startFolder);
                CreateShortcut(Path.Combine(startFolder, "Monolith.lnk"), targetExe, "", opts.InstallDir, targetIco, "Monolith");
                CreateShortcut(Path.Combine(startFolder, "Monolith Kaldır.lnk"), targetUninstaller, "--uninstall", opts.InstallDir, targetIco, "Monolith'ı Bilgisayardan Kaldır");
            }

            if (onProgress != null) onProgress(88, "Windows başlangıç ve Uygulamalar kaydı yazılıyor...");
            SetStartupRegistry(opts.LaunchAtStartup, targetExe);
            RegisterInInstalledApps(opts.InstallDir, targetExe, targetUninstaller);

            if (onProgress != null) onProgress(100, "Kurulum başarıyla tamamlandı!");
        }

        public static void Uninstall(string installDir, bool removeUserData, Action<int, string> onProgress)
        {
            if (onProgress != null) onProgress(15, "Monolith kapatılıyor ve Windows görev çubuğu geri yükleniyor...");
            StopRunningMonolith();

            if (onProgress != null) onProgress(35, "Windows başlangıç kaydı temizleniyor...");
            SetStartupRegistry(false, "");

            if (onProgress != null) onProgress(55, "Masaüstü ve Başlat Menüsü kısayolları kaldırılıyor...");
            try
            {
                string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Monolith.lnk");
                if (File.Exists(desktopLnk)) File.Delete(desktopLnk);
            }
            catch { }

            try
            {
                string startFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Monolith");
                if (Directory.Exists(startFolder)) Directory.Delete(startFolder, true);
            }
            catch { }

            if (onProgress != null) onProgress(72, "Windows Yüklü Uygulamalar kaydı siliniyor...");
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(UninstallRegPath, false);
            }
            catch { }

            if (removeUserData)
            {
                if (onProgress != null) onProgress(84, "Kullanıcı ayarları ve notlar temizleniyor...");
                try
                {
                    string appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Monolith");
                    if (Directory.Exists(appDataDir)) Directory.Delete(appDataDir, true);
                }
                catch { }
            }

            if (onProgress != null) onProgress(95, "Program dosyaları temizleniyor...");
            if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
            {
                TryDeleteFile(Path.Combine(installDir, "Monolith.exe"));
                TryDeleteFile(Path.Combine(installDir, "monolith.ico"));
                ScheduleSelfFolderCleanup(installDir);
            }

            RestoreWindowsTaskbar();
            if (onProgress != null) onProgress(100, "Monolith bilgisayardan başarıyla kaldırıldı!");
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void ScheduleSelfFolderCleanup(string dir)
        {
            try
            {
                string self = Process.GetCurrentProcess().MainModule.FileName;
                string fullDir = Path.GetFullPath(dir).TrimEnd('\\');
                if (Path.GetFullPath(self).StartsWith(fullDir, StringComparison.OrdinalIgnoreCase))
                {
                    // Spawn tiny detached cmd to remove MonolithUninstall.exe and empty folder right after exit
                    string args = "/C ping -n 3 127.0.0.1 >nul & rd /s /q \"" + fullDir + "\"";
                    ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", args);
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    Process.Start(psi);
                }
                else
                {
                    try { Directory.Delete(fullDir, true); } catch { }
                }
            }
            catch { }
        }

        private static void ExtractGzipResource(string resName, string destPath)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(resName))
            {
                if (s == null)
                    throw new InvalidOperationException("Gömülü kurulum paketi bulunamadı: " + resName);
                using (GZipStream gz = new GZipStream(s, CompressionMode.Decompress))
                using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                {
                    gz.CopyTo(fs);
                }
            }
        }

        private static void ExtractRawResource(string resName, string destPath)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream s = asm.GetManifestResourceStream(resName))
            {
                if (s == null) return;
                using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                {
                    s.CopyTo(fs);
                }
            }
        }

        private static void CreateShortcut(string lnkPath, string targetPath, string args, string workDir, string iconPath, string desc)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                Type scType = shortcut.GetType();
                scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                scType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { args ?? "" });
                scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workDir });
                if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
                    scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconPath + ",0" });
                scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { desc ?? "Monolith" });
                scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                Marshal.ReleaseComObject(shortcut);
                Marshal.ReleaseComObject(shell);
            }
            catch { }
        }

        private static void SetStartupRegistry(bool enable, string exePath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegPath, true))
                {
                    if (key == null) return;
                    if (enable && !string.IsNullOrEmpty(exePath))
                        key.SetValue("Monolith", "\"" + exePath + "\"");
                    else
                        key.DeleteValue("Monolith", false);
                }
            }
            catch { }
        }

        private static void RegisterInInstalledApps(string installDir, string exePath, string uninstallerPath)
        {
            try
            {
                long bytes = 0;
                if (File.Exists(exePath)) bytes += new FileInfo(exePath).Length;
                if (File.Exists(uninstallerPath)) bytes += new FileInfo(uninstallerPath).Length;
                int sizeKb = Math.Max(300, (int)(bytes / 1024));

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallRegPath))
                {
                    if (key == null) return;
                    key.SetValue("DisplayName", "Monolith");
                    key.SetValue("DisplayVersion", AppVersion);
                    key.SetValue("Publisher", "Monolith");
                    key.SetValue("DisplayIcon", exePath + ",0");
                    key.SetValue("InstallLocation", installDir);
                    key.SetValue("UninstallString", "\"" + uninstallerPath + "\" --uninstall");
                    key.SetValue("QuietUninstallString", "\"" + uninstallerPath + "\" --uninstall --silent");
                    key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                    key.SetValue("EstimatedSize", sizeKb, RegistryValueKind.DWord);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch { }
        }
    }
}
