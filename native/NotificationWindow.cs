using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace LocalReminder
{
    internal sealed class NotificationWindow : Window
    {
        private static readonly List<NotificationWindow> OpenWindows = new List<NotificationWindow>();
        private static readonly Queue<string> PendingFullscreenTitles = new Queue<string>();
        private static NotificationWindow activeFullscreen;
        private readonly DispatcherTimer closeTimer;
        private readonly bool fullScreen;
        private System.Drawing.Rectangle targetPixelArea;
        private double targetScale = 1;
        private string targetScreenKey;

        public NotificationWindow(string reminderTitle) : this(reminderTitle, "corner")
        {
        }

        public NotificationWindow(string reminderTitle, string popupMode)
        {
            fullScreen = popupMode == "fullscreen";
            Width = fullScreen ? 1024 : 360;
            Height = fullScreen ? 768 : 126;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = fullScreen;
            Topmost = true;
            UseLayoutRounding = true;

            Content = fullScreen ? CreateFullscreenContent(reminderTitle) : CreateCornerContent(reminderTitle);

            SourceInitialized += delegate
            {
                CaptureTargetScreen();
                if (fullScreen)
                    PositionFullscreen();
                else
                    CaptureTargetDpi();
            };
            Loaded += delegate
            {
                if (fullScreen)
                {
                    PositionFullscreen();
                }
                else
                {
                    OpenWindows.Add(this);
                    RepositionAll();
                }
                closeTimer.Start();
            };
            Closed += delegate
            {
                closeTimer.Stop();
                if (fullScreen)
                {
                    if (object.ReferenceEquals(activeFullscreen, this))
                    {
                        activeFullscreen = null;
                        ShowNextFullscreen();
                    }
                }
                else
                {
                    OpenWindows.Remove(this);
                    RepositionAll();
                }
            };

            closeTimer = new DispatcherTimer();
            closeTimer.Interval = TimeSpan.FromSeconds(10);
            closeTimer.Tick += delegate
            {
                closeTimer.Stop();
                Close();
            };
        }

        internal static void ShowReminder(string reminderTitle, string popupMode)
        {
            if (popupMode != "fullscreen")
            {
                new NotificationWindow(reminderTitle, "corner").Show();
                return;
            }

            PendingFullscreenTitles.Enqueue(reminderTitle);
            ShowNextFullscreen();
        }

        private static void ShowNextFullscreen()
        {
            if (activeFullscreen != null || PendingFullscreenTitles.Count == 0) return;
            if (Application.Current == null || Application.Current.Dispatcher.HasShutdownStarted)
            {
                PendingFullscreenTitles.Clear();
                return;
            }

            activeFullscreen = new NotificationWindow(PendingFullscreenTitles.Dequeue(), "fullscreen");
            activeFullscreen.Show();
        }

        private UIElement CreateCornerContent(string reminderTitle)
        {
            Border card = new Border();
            card.Background = Brushes.White;
            card.BorderBrush = Theme.Line;
            card.BorderThickness = new Thickness(1);
            card.CornerRadius = new CornerRadius(14);
            card.Padding = new Thickness(18);
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 24,
                ShadowDepth = 5,
                Opacity = 0.20,
                Color = Color.FromRgb(18, 50, 44)
            };

            Grid layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });

            Border clockBox = new Border();
            clockBox.Width = 48;
            clockBox.Height = 48;
            clockBox.Background = Theme.Green;
            clockBox.CornerRadius = new CornerRadius(13);
            clockBox.Child = CreateClock();
            Grid.SetColumn(clockBox, 0);
            layout.Children.Add(clockBox);

            StackPanel copy = new StackPanel();
            copy.Margin = new Thickness(13, 2, 8, 0);
            TextBlock label = Theme.Text("现在该做", 11, Theme.Muted, FontWeights.Bold);
            label.Margin = new Thickness(0, 0, 0, 5);
            TextBlock title = Theme.Text(reminderTitle, 16, Theme.Ink, FontWeights.Bold);
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.MaxHeight = 48;
            copy.Children.Add(label);
            copy.Children.Add(title);
            Grid.SetColumn(copy, 1);
            layout.Children.Add(copy);

            Button close = new Button();
            close.Content = "×";
            close.Width = 28;
            close.Height = 28;
            close.Padding = new Thickness(0);
            close.BorderThickness = new Thickness(0);
            close.Background = Theme.Brush("#EFF3F2");
            close.Foreground = Theme.Muted;
            close.FontSize = 18;
            close.Cursor = System.Windows.Input.Cursors.Hand;
            close.VerticalAlignment = VerticalAlignment.Top;
            close.Click += delegate { Close(); };
            Grid.SetColumn(close, 2);
            layout.Children.Add(close);

            card.Child = layout;
            return card;
        }

        private UIElement CreateFullscreenContent(string reminderTitle)
        {
            Border overlay = new Border();
            overlay.Background = new SolidColorBrush(Color.FromArgb(238, 15, 28, 25));

            Border card = new Border();
            card.Width = 620;
            card.MaxWidth = 620;
            card.Padding = new Thickness(44, 40, 44, 36);
            card.Background = Brushes.White;
            card.CornerRadius = new CornerRadius(22);
            card.HorizontalAlignment = HorizontalAlignment.Center;
            card.VerticalAlignment = VerticalAlignment.Center;

            StackPanel content = new StackPanel();
            Border clockBox = new Border
            {
                Width = 64,
                Height = 64,
                Background = Theme.Green,
                CornerRadius = new CornerRadius(18),
                Child = CreateClock(),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            content.Children.Add(clockBox);

            TextBlock label = Theme.Text("现在该做", 13, Theme.Green, FontWeights.Bold);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.Margin = new Thickness(0, 22, 0, 10);
            content.Children.Add(label);

            TextBlock title = Theme.Text(reminderTitle, 30, Theme.Ink, FontWeights.Bold);
            title.TextAlignment = TextAlignment.Center;
            title.TextWrapping = TextWrapping.Wrap;
            title.MaxHeight = 150;
            content.Children.Add(title);

            Button close = Theme.Button("知道了", true);
            close.Width = 126;
            close.HorizontalAlignment = HorizontalAlignment.Center;
            close.Margin = new Thickness(0, 28, 0, 0);
            close.Click += delegate { Close(); };
            content.Children.Add(close);

            card.Child = content;
            overlay.Child = card;
            return overlay;
        }

        private static Canvas CreateClock()
        {
            Canvas canvas = new Canvas();
            canvas.Width = 32;
            canvas.Height = 32;
            Ellipse ring = new Ellipse
            {
                Width = 24,
                Height = 24,
                Stroke = Brushes.White,
                StrokeThickness = 2.4
            };
            Canvas.SetLeft(ring, 4);
            Canvas.SetTop(ring, 4);
            canvas.Children.Add(ring);
            Line minute = new Line { X1 = 16, Y1 = 9, X2 = 16, Y2 = 17, Stroke = Brushes.White, StrokeThickness = 2.4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            Line hour = new Line { X1 = 16, Y1 = 17, X2 = 22, Y2 = 21, Stroke = Brushes.White, StrokeThickness = 2.4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            canvas.Children.Add(minute);
            canvas.Children.Add(hour);
            return canvas;
        }

        private static void RepositionAll()
        {
            Dictionary<string, int> screenCounts = new Dictionary<string, int>();
            foreach (NotificationWindow window in OpenWindows)
            {
                int ordinal;
                if (!screenCounts.TryGetValue(window.targetScreenKey, out ordinal)) ordinal = 0;
                screenCounts[window.targetScreenKey] = ordinal + 1;

                System.Drawing.Rectangle placement = NotificationPlacement.CalculateCorner(
                    window.targetPixelArea,
                    window.targetScale,
                    ordinal);
                SetWindowPos(
                    new WindowInteropHelper(window).Handle,
                    IntPtr.Zero,
                    placement.Left,
                    placement.Top,
                    placement.Width,
                    placement.Height,
                    SetWindowPosFlags.NoZOrder | SetWindowPosFlags.NoActivate);
            }
        }

        private void CaptureTargetScreen()
        {
            Forms.Screen target = Forms.Screen.FromPoint(Forms.Cursor.Position);
            targetScreenKey = target.DeviceName;
            targetPixelArea = fullScreen ? target.Bounds : target.WorkingArea;
        }

        private void CaptureTargetDpi()
        {
            WindowInteropHelper helper = new WindowInteropHelper(this);
            int initialWidth = Math.Min(360, targetPixelArea.Width);
            int initialHeight = Math.Min(126, targetPixelArea.Height);
            SetWindowPos(
                helper.Handle,
                IntPtr.Zero,
                targetPixelArea.Right - initialWidth,
                targetPixelArea.Top,
                initialWidth,
                initialHeight,
                SetWindowPosFlags.NoZOrder | SetWindowPosFlags.NoActivate);

            try
            {
                uint dpi = GetDpiForWindow(helper.Handle);
                if (dpi > 0) targetScale = dpi / 96.0;
            }
            catch (EntryPointNotFoundException)
            {
                PresentationSource source = PresentationSource.FromVisual(this);
                if (source != null && source.CompositionTarget != null)
                    targetScale = source.CompositionTarget.TransformToDevice.M11;
            }
        }

        private void PositionFullscreen()
        {
            WindowInteropHelper helper = new WindowInteropHelper(this);
            bool positioned = SetWindowPos(
                helper.Handle,
                IntPtr.Zero,
                targetPixelArea.Left,
                targetPixelArea.Top,
                targetPixelArea.Width,
                targetPixelArea.Height,
                SetWindowPosFlags.NoZOrder | SetWindowPosFlags.NoActivate);
            if (positioned) return;

            PresentationSource source = PresentationSource.FromVisual(this);
            if (source == null || source.CompositionTarget == null) return;
            Matrix transform = source.CompositionTarget.TransformFromDevice;
            Point topLeft = transform.Transform(new Point(targetPixelArea.Left, targetPixelArea.Top));
            Point bottomRight = transform.Transform(new Point(targetPixelArea.Right, targetPixelArea.Bottom));
            Left = topLeft.X;
            Top = topLeft.Y;
            Width = bottomRight.X - topLeft.X;
            Height = bottomRight.Y - topLeft.Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            SetWindowPosFlags flags);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr window);

        [Flags]
        private enum SetWindowPosFlags : uint
        {
            NoZOrder = 0x0004,
            NoActivate = 0x0010
        }
    }
}
