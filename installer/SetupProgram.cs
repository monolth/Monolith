using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace MonolithSetup
{
    public static class SetupProgram
    {
        [STAThread]
        public static void Main(string[] args)
        {
            bool uninstall = false;
            bool silent = false;
            bool removeData = false;
            string customDir = null;

            string selfName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
            if (selfName.IndexOf("Uninstall", StringComparison.OrdinalIgnoreCase) >= 0)
                uninstall = true;

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string a = args[i] ?? "";
                    if (a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) || a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase))
                        uninstall = true;
                    else if (a.Equals("--silent", StringComparison.OrdinalIgnoreCase) || a.Equals("/S", StringComparison.OrdinalIgnoreCase))
                        silent = true;
                    else if (a.Equals("--remove-data", StringComparison.OrdinalIgnoreCase))
                        removeData = true;
                    else if (a.StartsWith("--dir=", StringComparison.OrdinalIgnoreCase))
                        customDir = a.Substring(6).Trim('"');
                }
            }

            if (silent)
            {
                if (uninstall)
                {
                    string dir = !string.IsNullOrEmpty(customDir) ? customDir : InstallerCore.GetInstalledLocation();
                    InstallerCore.Uninstall(dir, removeData, null);
                }
                else
                {
                    InstallOptions opts = new InstallOptions();
                    if (!string.IsNullOrEmpty(customDir)) opts.InstallDir = customDir;
                    opts.LaunchAfterInstall = false;
                    InstallerCore.Install(opts, null);
                }
                return;
            }

            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            SetupWindow win = new SetupWindow(uninstall, customDir);
            app.Run(win);
        }
    }
}
