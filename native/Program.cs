using System;
using System.Threading;
using System.Windows;

namespace LocalReminder
{
    internal static class Program
    {
        [STAThread]
        public static int Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "LocalReminder.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("定时提醒已经在运行。", "定时提醒", MessageBoxButton.OK, MessageBoxImage.Information);
                    return 0;
                }

                try
                {
                    Application app = new Application();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                    MainWindow window = new MainWindow();
                    app.Run(window);
                    return 0;
                }
                catch (Exception error)
                {
                    MessageBox.Show("程序无法启动：" + error.Message, "定时提醒", MessageBoxButton.OK, MessageBoxImage.Error);
                    return 1;
                }
            }
        }
    }
}
