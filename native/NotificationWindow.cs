using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace LocalReminder
{
    internal sealed class NotificationWindow : Window
    {
        private static readonly List<NotificationWindow> OpenWindows = new List<NotificationWindow>();
        private readonly DispatcherTimer closeTimer;
        private Rect targetWorkArea;
        private string targetScreenKey;

        public NotificationWindow(string reminderTitle)
        {
            Width = 360;
            Height = 126;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            UseLayoutRounding = true;

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
            Content = card;

            Loaded += delegate
            {
                CaptureTargetScreen();
                OpenWindows.Add(this);
                RepositionAll();
            };
            Closed += delegate
            {
                OpenWindows.Remove(this);
                RepositionAll();
            };

            closeTimer = new DispatcherTimer();
            closeTimer.Interval = TimeSpan.FromSeconds(10);
            closeTimer.Tick += delegate
            {
                closeTimer.Stop();
                Close();
            };
            closeTimer.Start();
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

                Rect workArea = window.targetWorkArea;
                int maximumRows = Math.Max(1, (int)Math.Floor((workArea.Height - 36) / (window.Height + 10)));
                int row = ordinal % maximumRows;
                int column = ordinal / maximumRows;
                window.Left = Math.Max(
                    workArea.Left + 18,
                    workArea.Right - window.Width - 18 - column * (window.Width + 10));
                window.Top = workArea.Top + 18 + row * (window.Height + 10);
            }
        }

        private void CaptureTargetScreen()
        {
            Forms.Screen target = Forms.Screen.FromPoint(Forms.Cursor.Position);
            targetScreenKey = target.DeviceName;
            System.Drawing.Rectangle pixels = target.WorkingArea;
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source != null && source.CompositionTarget != null)
            {
                Matrix transform = source.CompositionTarget.TransformFromDevice;
                Point topLeft = transform.Transform(new Point(pixels.Left, pixels.Top));
                Point bottomRight = transform.Transform(new Point(pixels.Right, pixels.Bottom));
                targetWorkArea = new Rect(topLeft, bottomRight);
            }
            else
            {
                targetWorkArea = SystemParameters.WorkArea;
            }
        }
    }
}
