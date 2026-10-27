using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LocalReminder
{
    internal sealed class ScreenPowerMonitor : IDisposable
    {
        private const int WmPowerBroadcast = 0x0218;
        private const int PowerSettingChange = 0x8013;
        private const int Suspend = 0x0004;
        private const int ResumeSuspend = 0x0007;
        private static readonly Guid ConsoleDisplayState = new Guid("6FE69556-704A-47A0-8F24-C28D936FDA47");

        private readonly Action<bool> stateChanged;
        private readonly HwndSource source;
        private IntPtr registration;

        internal ScreenPowerMonitor(Window window, Action<bool> stateChanged)
        {
            if (window == null) throw new ArgumentNullException("window");
            if (stateChanged == null) throw new ArgumentNullException("stateChanged");
            this.stateChanged = stateChanged;

            IntPtr handle = new WindowInteropHelper(window).Handle;
            source = HwndSource.FromHwnd(handle);
            if (source == null) throw new InvalidOperationException("无法连接窗口消息");
            source.AddHook(WindowProcedure);

            Guid setting = ConsoleDisplayState;
            registration = RegisterPowerSettingNotification(handle, ref setting, 0);
            if (registration == IntPtr.Zero)
            {
                source.RemoveHook(WindowProcedure);
                throw new Win32Exception(Marshal.GetLastWin32Error(), "无法监听屏幕亮灭状态");
            }
        }

        public void Dispose()
        {
            if (registration != IntPtr.Zero)
            {
                UnregisterPowerSettingNotification(registration);
                registration = IntPtr.Zero;
            }
            source.RemoveHook(WindowProcedure);
        }

        private IntPtr WindowProcedure(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message != WmPowerBroadcast) return IntPtr.Zero;

            int powerEvent = wParam.ToInt32();
            if (powerEvent == Suspend)
            {
                stateChanged(false);
            }
            else if (powerEvent == ResumeSuspend)
            {
                stateChanged(true);
            }
            else if (powerEvent == PowerSettingChange && lParam != IntPtr.Zero)
            {
                Guid setting = (Guid)Marshal.PtrToStructure(lParam, typeof(Guid));
                int dataLength = Marshal.ReadInt32(lParam, 16);
                if (setting == ConsoleDisplayState && dataLength >= 4)
                {
                    int displayState = Marshal.ReadInt32(lParam, 20);
                    stateChanged(displayState != 0);
                }
            }
            return IntPtr.Zero;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr RegisterPowerSettingNotification(
            IntPtr recipient,
            ref Guid powerSettingGuid,
            int flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterPowerSettingNotification(IntPtr handle);
    }
}
