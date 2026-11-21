using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace Monolith
{
    public static class Program
    {
        private static Mutex _singleInstance;

        [STAThread]
        public static void Main(string[] args)
        {
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "--restore-taskbar", StringComparison.OrdinalIgnoreCase))
                    {
                        StopOtherInstancesAndRestoreTaskbar();
                        return;
                    }
                }
            }

            bool createdNew;
            _singleInstance = new Mutex(true, "Monolith_SingleInstance_v1_Mutex", out createdNew);
            if (!createdNew)
            {
                return;
            }

            // Always guarantee native Windows taskbar restoration on any unexpected exit!
            AppDomain.CurrentDomain.UnhandledException += delegate
            {
                NativeWin32.RestoreNativeTaskbars();
            };
            SystemEvents.SessionEnding += delegate
            {
                NativeWin32.RestoreNativeTaskbars();
            };

            try
            {
                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                app.DispatcherUnhandledException += delegate(object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
                {
                    NativeWin32.RestoreNativeTaskbars();
                    e.Handled = true;
                };

                DockWindow dock = new DockWindow();
                app.Run(dock);
            }
            finally
            {
                NativeWin32.RestoreNativeTaskbars();
                if (_singleInstance != null)
                {
                    try { _singleInstance.ReleaseMutex(); } catch { }
                    _singleInstance.Dispose();
                }
            }
        }

        private static void StopOtherInstancesAndRestoreTaskbar()
        {
            int selfPid = Process.GetCurrentProcess().Id;
            try
            {
                Process[] procs = Process.GetProcessesByName("Monolith");
                for (int i = 0; i < procs.Length; i++)
                {
                    using (Process p = procs[i])
                    {
                        if (p.Id == selfPid) continue;
                        try
                        {
                            p.CloseMainWindow();
                            if (!p.WaitForExit(1400))
                                p.Kill();
                            p.WaitForExit(1000);
                        }
                        catch { }
                    }
                }
            }
            catch { }
            NativeWin32.RestoreNativeTaskbars();
        }
    }
}
