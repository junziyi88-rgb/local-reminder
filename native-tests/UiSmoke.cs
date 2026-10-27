using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LocalReminder;
using Forms = System.Windows.Forms;

internal static class UiSmoke
{
    [STAThread]
    public static int Main()
    {
        ScreenUsageTracker usageTracker = new ScreenUsageTracker(TimeSpan.Zero);
        TimeSpan usage = usageTracker.GetUsage(TimeSpan.FromMinutes(30));
        if (usage != TimeSpan.FromMinutes(30)) throw new InvalidOperationException("亮屏累计错误");

        usageTracker.SetScreenOn(false, TimeSpan.FromMinutes(30));
        usage = usageTracker.GetUsage(TimeSpan.FromMinutes(33));
        if (usage != TimeSpan.FromMinutes(30)) throw new InvalidOperationException("短息屏没有暂停累计");
        if (usageTracker.IsScreenOn) throw new InvalidOperationException("息屏状态记录错误");

        usageTracker.SetScreenOn(true, TimeSpan.FromMinutes(33));
        usage = usageTracker.GetUsage(TimeSpan.FromMinutes(63));
        if (usage != TimeSpan.FromMinutes(60)) throw new InvalidOperationException("短息屏后没有继续累计");
        if (!usageTracker.IsScreenOn) throw new InvalidOperationException("亮屏状态记录错误");

        long originalSession = usageTracker.SessionId;
        usageTracker.SetScreenOn(false, TimeSpan.FromMinutes(63));
        usage = usageTracker.GetUsage(TimeSpan.FromMinutes(68));
        if (usage != TimeSpan.Zero || usageTracker.SessionId == originalSession)
            throw new InvalidOperationException("息屏满五分钟没有清零");

        ScreenReminderProgress progress = new ScreenReminderProgress();
        Reminder screenReminder = new Reminder
        {
            Id = "screen-test",
            Title = "休息",
            Enabled = true,
            ScheduleType = "screenTime",
            IntervalMinutes = 20
        };
        Func<double, long, bool> checkDue = delegate(double minutes, long session)
        {
            return progress.IsDue(screenReminder, TimeSpan.FromMinutes(minutes), session);
        };
        if (checkDue(19, 0)) throw new InvalidOperationException("亮屏时长未到却触发提醒");
        if (!checkDue(20, 0)) throw new InvalidOperationException("亮屏时长达到后没有触发提醒");
        if (checkDue(21, 0)) throw new InvalidOperationException("同一周期重复触发提醒");
        if (!checkDue(40, 0)) throw new InvalidOperationException("继续亮屏没有再次提醒");
        if (checkDue(0, 1) || !checkDue(20, 1))
            throw new InvalidOperationException("长息屏后没有重新计时");

        ScreenUsageTracker offAtThreshold = new ScreenUsageTracker(TimeSpan.Zero);
        ScreenReminderProgress offProgress = new ScreenReminderProgress();
        offAtThreshold.SetScreenOn(false, TimeSpan.FromMinutes(20));
        TimeSpan pausedUsage = offAtThreshold.GetUsage(TimeSpan.FromMinutes(23));
        if (offAtThreshold.IsScreenOn && offProgress.IsDue(screenReminder, pausedUsage, offAtThreshold.SessionId))
            throw new InvalidOperationException("息屏时不应消耗提醒");
        offAtThreshold.SetScreenOn(true, TimeSpan.FromMinutes(23));
        if (!offAtThreshold.IsScreenOn || !offProgress.IsDue(screenReminder, pausedUsage, offAtThreshold.SessionId))
            throw new InvalidOperationException("短息屏恢复后没有触发提醒");
        if (offProgress.IsDue(screenReminder, pausedUsage, offAtThreshold.SessionId))
            throw new InvalidOperationException("恢复亮屏后重复触发提醒");

        screenReminder.IntervalMinutes = 60;
        progress.Reset(screenReminder, TimeSpan.FromMinutes(40), 1);
        if (checkDue(99, 1) || !checkDue(100, 1))
            throw new InvalidOperationException("改长时长后没有从修改时重新计时");
        screenReminder.IntervalMinutes = 20;
        progress.Reset(screenReminder, TimeSpan.FromMinutes(100), 1);
        if (checkDue(119, 1) || !checkDue(120, 1))
            throw new InvalidOperationException("改短时长后没有从修改时重新计时");

        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "local-reminder-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        Application app = new Application();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        MainWindow window = new MainWindow(Path.Combine(temporaryDirectory, "reminders.json"), false);
        window.Show();
        window.UpdateLayout();
        using (ScreenPowerMonitor monitor = new ScreenPowerMonitor(window, delegate(bool screenOn) { }))
        {
        }
        Render(window, Path.Combine(temporaryDirectory, "native-ui.png"));

        ReminderEditorWindow editor = new ReminderEditorWindow(null);
        editor.Show();
        editor.UpdateLayout();
        FieldInfo scheduleField = typeof(ReminderEditorWindow).GetField(
            "scheduleTypeInput",
            BindingFlags.Instance | BindingFlags.NonPublic);
        System.Windows.Controls.ComboBox scheduleOptions = scheduleField == null
            ? null
            : scheduleField.GetValue(editor) as System.Windows.Controls.ComboBox;
        if (scheduleOptions == null || !scheduleOptions.Items.Contains("连续亮屏"))
            throw new InvalidOperationException("编辑窗口缺少连续亮屏选项");
        Render(editor, Path.Combine(temporaryDirectory, "native-editor.png"));
        editor.Close();

        System.Drawing.Rectangle scaledPlacement = NotificationPlacement.CalculateCorner(
            new System.Drawing.Rectangle(1920, 0, 1920, 1080),
            1.5,
            0);
        if (scaledPlacement != new System.Drawing.Rectangle(3273, 27, 540, 189))
            throw new InvalidOperationException("150% 缩放的右上角布局计算错误");

        NotificationWindow notification = new NotificationWindow("喝水并活动一下", "corner");
        notification.Show();
        notification.UpdateLayout();
        if (notification.ActualWidth > 500) throw new InvalidOperationException("右上角通知尺寸异常");
        Render(notification, Path.Combine(temporaryDirectory, "native-notification.png"));
        notification.Close();

        System.Drawing.Rectangle expectedScreen = Forms.Screen.FromPoint(Forms.Cursor.Position).Bounds;
        NotificationWindow fullscreen = new NotificationWindow("现在休息一下", "fullscreen");
        fullscreen.Show();
        fullscreen.UpdateLayout();
        if (fullscreen.ActualWidth < 800 || fullscreen.ActualHeight < 500)
            throw new InvalidOperationException("全屏通知没有覆盖屏幕");
        NativeRect actualScreen;
        if (!GetWindowRect(new WindowInteropHelper(fullscreen).Handle, out actualScreen) ||
            Math.Abs(actualScreen.Left - expectedScreen.Left) > 1 ||
            Math.Abs(actualScreen.Top - expectedScreen.Top) > 1 ||
            Math.Abs(actualScreen.Right - expectedScreen.Right) > 1 ||
            Math.Abs(actualScreen.Bottom - expectedScreen.Bottom) > 1)
            throw new InvalidOperationException("全屏通知没有精确覆盖鼠标所在屏幕");
        Render(fullscreen, Path.Combine(temporaryDirectory, "native-fullscreen-notification.png"));
        fullscreen.Close();

        NotificationWindow.ShowReminder("第一条全屏提醒", "fullscreen");
        NotificationWindow.ShowReminder("第二条全屏提醒", "fullscreen");
        NotificationWindow queuedFullscreen = FindVisibleFullscreen();
        if (queuedFullscreen == null || CountVisibleFullscreen() != 1)
            throw new InvalidOperationException("多个全屏提醒没有按顺序显示");
        queuedFullscreen.Close();
        queuedFullscreen = FindVisibleFullscreen();
        if (queuedFullscreen == null || CountVisibleFullscreen() != 1)
            throw new InvalidOperationException("关闭首条后没有显示下一条全屏提醒");
        queuedFullscreen.Close();

        window.Close();
        if (window.IsVisible) throw new InvalidOperationException("关闭主窗口后没有隐藏");
        if (new FileInfo(Path.Combine(temporaryDirectory, "native-ui.png")).Length == 0)
            throw new InvalidOperationException("主窗口渲染结果为空");
        if (new FileInfo(Path.Combine(temporaryDirectory, "native-editor.png")).Length == 0)
            throw new InvalidOperationException("编辑窗口渲染结果为空");
        if (new FileInfo(Path.Combine(temporaryDirectory, "native-notification.png")).Length == 0)
            throw new InvalidOperationException("通知窗口渲染结果为空");
        if (new FileInfo(Path.Combine(temporaryDirectory, "native-fullscreen-notification.png")).Length == 0)
            throw new InvalidOperationException("全屏通知渲染结果为空");
        if (Environment.GetEnvironmentVariable("KEEP_UI_SMOKE_ARTIFACTS") == "1")
            Console.WriteLine("UI_ARTIFACTS " + temporaryDirectory);
        else
            Directory.Delete(temporaryDirectory, true);
        Console.WriteLine("UI_SMOKE_OK");
        return 0;
    }

    private static int CountVisibleFullscreen()
    {
        int count = 0;
        foreach (Window item in Application.Current.Windows)
            if (item is NotificationWindow && item.IsVisible && item.ActualWidth > 500) count++;
        return count;
    }

    private static NotificationWindow FindVisibleFullscreen()
    {
        foreach (Window item in Application.Current.Windows)
            if (item is NotificationWindow && item.IsVisible && item.ActualWidth > 500)
                return (NotificationWindow)item;
        return null;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rectangle);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
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
