using System;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace Monolith
{
    public static class Program
    {
        private static Mutex _singleInstance;

        [STAThread]
        public static void Main()
        {
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
    }
}
