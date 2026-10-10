using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LocalReminder;

internal static class UiSmoke
{
    [STAThread]
    public static int Main()
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "local-reminder-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        Application app = new Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        MainWindow window = new MainWindow(Path.Combine(temporaryDirectory, "reminders.json"), false);
        window.Show();
        window.UpdateLayout();
        Render(window, Path.Combine(temporaryDirectory, "native-ui.png"));

        NotificationWindow notification = new NotificationWindow("喝水并活动一下");
        notification.Show();
        notification.UpdateLayout();
        Render(notification, Path.Combine(temporaryDirectory, "native-notification.png"));
        notification.Close();

        window.Close();
        if (window.IsVisible) throw new InvalidOperationException("关闭主窗口后没有隐藏");
        if (new FileInfo(Path.Combine(temporaryDirectory, "native-ui.png")).Length == 0)
            throw new InvalidOperationException("主窗口渲染结果为空");
        if (new FileInfo(Path.Combine(temporaryDirectory, "native-notification.png")).Length == 0)
            throw new InvalidOperationException("通知窗口渲染结果为空");
        Directory.Delete(temporaryDirectory, true);
        Console.WriteLine("UI_SMOKE_OK");
        return 0;
    }

    private static void Render(Window window, string path)
    {
        int width = (int)Math.Ceiling(window.ActualWidth);
        int height = (int)Math.Ceiling(window.ActualHeight);
        RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        PngBitmapEncoder encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream stream = File.Create(path)) encoder.Save(stream);
    }
}
