using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using FreePin.Native;
using FreePin.Services;

namespace FreePin
{
    internal static class Program
    {
        private const string MutexName = @"Local\FreePin.SingleInstance.9C4A56D2";

        [STAThread]
        private static void Main(string[] args)
        {
            NativeMethods.EnableDpiAwareness();

            bool showSettings = false;
            if (args != null)
            {
                foreach (string arg in args)
                {
                    if (string.Equals(arg, "--settings", StringComparison.OrdinalIgnoreCase))
                        showSettings = true;
                }
            }

            bool created;
            using (var mutex = new Mutex(true, MutexName, out created))
            {
                if (!created)
                {
                    MessageBox.Show("FreePin 已经在运行中。\n可以在系统托盘找到它。", "FreePin",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var application = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };

                var host = new AppHost(application);
                application.Startup += (s, e) =>
                {
                    host.Start();
                    if (showSettings)
                        application.Dispatcher.BeginInvoke(new Action(host.OpenSettings));
                };
                application.DispatcherUnhandledException += (s, e) =>
                {
                    Log.Error("UI 线程未处理异常", e.Exception);
                    try
                    {
                        host.Notify("FreePin 出错", e.Exception.Message);
                    }
                    catch
                    {
                        // ignored
                    }
                    e.Handled = true;
                };
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    Log.Error("后台线程未处理异常", e.ExceptionObject as Exception);
                };
                application.Exit += (s, e) => host.Dispose();

                application.Run();
            }
        }
    }
}
