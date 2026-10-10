using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace LocalReminder
{
    internal sealed class MainWindow : Window
    {
        private readonly ReminderStore store;
        private readonly StackPanel listPanel;
        private readonly TextBlock countText;
        private readonly DispatcherTimer schedulerTimer;
        private Forms.NotifyIcon trayIcon;
        private List<Reminder> reminders;
        private bool allowExit;
        private bool storageErrorShown;

        public MainWindow() : this(null, true)
        {
        }

        internal MainWindow(string dataFilePath, bool enableBackground)
        {
            Title = "定时提醒";
            Width = 920;
            Height = 680;
            MinWidth = 760;
            MinHeight = 540;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Theme.Canvas;
            FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");
            UseLayoutRounding = true;

            string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalReminder");
            string resolvedDataPath = dataFilePath ?? Path.Combine(dataDirectory, "reminders.json");
            store = new ReminderStore(resolvedDataPath);
            reminders = store.Load();

            Grid root = new Grid();
            root.Margin = new Thickness(30);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid header = new Grid();
            header.Margin = new Thickness(0, 0, 0, 24);
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel brand = new StackPanel();
            TextBlock title = Theme.Text("定时提醒", 27, Theme.Ink, FontWeights.Bold);
            TextBlock subtitle = Theme.Text("需要做的事，到点自然出现", 13, Theme.Muted, FontWeights.Normal);
            subtitle.Margin = new Thickness(0, 5, 0, 0);
            brand.Children.Add(title);
            brand.Children.Add(subtitle);
            header.Children.Add(brand);

            Button add = Theme.Button("＋  新建提醒", true);
            add.VerticalAlignment = VerticalAlignment.Center;
            add.Click += delegate { AddReminder(); };
            Grid.SetColumn(add, 1);
            header.Children.Add(add);
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            Border panel = new Border();
            panel.Background = System.Windows.Media.Brushes.White;
            panel.BorderBrush = Theme.Line;
            panel.BorderThickness = new Thickness(1);
            panel.CornerRadius = new CornerRadius(18);
            panel.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 35,
                ShadowDepth = 8,
                Opacity = 0.12,
                Color = System.Windows.Media.Color.FromArgb(255, 24, 54, 49)
            };

            Grid panelGrid = new Grid();
            panelGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panelGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid panelHeader = new Grid();
            panelHeader.Margin = new Thickness(24, 20, 24, 18);
            panelHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panelHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel panelHeading = new StackPanel();
            TextBlock eyebrow = Theme.Text("提醒事项", 11, Theme.Green, FontWeights.Bold);
            eyebrow.Margin = new Thickness(0, 0, 0, 5);
            panelHeading.Children.Add(eyebrow);
            panelHeading.Children.Add(Theme.Text("全部提醒", 21, Theme.Ink, FontWeights.Bold));
            panelHeader.Children.Add(panelHeading);
            countText = Theme.Text("0 项", 12, Theme.Green, FontWeights.Bold);
            Border countBadge = new Border();
            countBadge.Background = Theme.GreenLight;
            countBadge.CornerRadius = new CornerRadius(14);
            countBadge.Padding = new Thickness(11, 6, 11, 6);
            countBadge.VerticalAlignment = VerticalAlignment.Center;
            countBadge.Child = countText;
            Grid.SetColumn(countBadge, 1);
            panelHeader.Children.Add(countBadge);
            panelGrid.Children.Add(panelHeader);

            Border divider = new Border();
            divider.BorderBrush = Theme.Line;
            divider.BorderThickness = new Thickness(0, 1, 0, 0);
            Grid.SetRow(divider, 1);
            panelGrid.Children.Add(divider);

            listPanel = new StackPanel();
            ScrollViewer scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.Content = listPanel;
            Grid.SetRow(scroll, 1);
            panelGrid.Children.Add(scroll);
            panel.Child = panelGrid;
            Grid.SetRow(panel, 1);
            root.Children.Add(panel);
            Content = root;

            Closing += OnClosing;

            if (enableBackground)
            {
                trayIcon = new Forms.NotifyIcon();
                trayIcon.Text = "定时提醒";
                trayIcon.Visible = true;
                trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
                Forms.ToolStripMenuItem openItem = new Forms.ToolStripMenuItem("打开定时提醒");
                Forms.ToolStripMenuItem exitItem = new Forms.ToolStripMenuItem("退出");
                openItem.Click += delegate { Dispatcher.Invoke(ShowFromTray); };
                exitItem.Click += delegate { Dispatcher.Invoke(ExitApplication); };
                menu.Items.Add(openItem);
                menu.Items.Add(new Forms.ToolStripSeparator());
                menu.Items.Add(exitItem);
                trayIcon.ContextMenuStrip = menu;
                trayIcon.DoubleClick += delegate { Dispatcher.Invoke(ShowFromTray); };
            }

            ReconcileOverdue();
            RenderReminders();

            schedulerTimer = new DispatcherTimer();
            schedulerTimer.Interval = TimeSpan.FromSeconds(1);
            schedulerTimer.Tick += delegate { CheckDueReminders(); };
            if (enableBackground) schedulerTimer.Start();
        }

        private void AddReminder()
        {
            ReminderEditorWindow editor = new ReminderEditorWindow(null);
            editor.Owner = this;
            if (editor.ShowDialog() == true)
            {
                List<Reminder> candidate = new List<Reminder>(reminders);
                candidate.Add(editor.ResultReminder);
                TryCommit(candidate);
            }
        }

        private void EditReminder(Reminder reminder)
        {
            ReminderEditorWindow editor = new ReminderEditorWindow(reminder);
            editor.Owner = this;
            if (editor.ShowDialog() == true)
            {
                List<Reminder> candidate = new List<Reminder>(reminders);
                int index = candidate.FindIndex(delegate(Reminder item) { return item.Id == reminder.Id; });
                if (index >= 0) candidate[index] = editor.ResultReminder;
                TryCommit(candidate);
            }
        }

        private void DeleteReminder(Reminder reminder)
        {
            MessageBoxResult answer = MessageBox.Show(
                this,
                "确定删除“" + reminder.Title + "”？",
                "删除提醒",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
            List<Reminder> candidate = new List<Reminder>(reminders);
            candidate.RemoveAll(delegate(Reminder item) { return item.Id == reminder.Id; });
            TryCommit(candidate);
        }

        private void ToggleReminder(Reminder reminder, bool enabled)
        {
            List<Reminder> candidate = CloneReminders(reminders);
            Reminder changed = candidate.Find(delegate(Reminder item) { return item.Id == reminder.Id; });
            if (changed == null) return;
            changed.Enabled = enabled;
            changed.UpdatedAt = DateTime.Now;
            changed.NextTriggerAt = enabled ? (DateTime?)ReminderScheduler.ComputeNext(changed, DateTime.Now) : null;
            TryCommit(candidate);
        }

        private void RenderReminders()
        {
            listPanel.Children.Clear();
            countText.Text = reminders.Count + " 项";
            if (reminders.Count == 0)
            {
                StackPanel empty = new StackPanel();
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                empty.VerticalAlignment = VerticalAlignment.Center;
                empty.Margin = new Thickness(30, 105, 30, 80);
                TextBlock symbol = Theme.Text("◷", 52, Theme.Green, FontWeights.Normal);
                symbol.HorizontalAlignment = HorizontalAlignment.Center;
                TextBlock heading = Theme.Text("还没有提醒事项", 17, Theme.Ink, FontWeights.Bold);
                heading.HorizontalAlignment = HorizontalAlignment.Center;
                heading.Margin = new Thickness(0, 10, 0, 6);
                TextBlock copy = Theme.Text("新建第一条提醒，到点会在右上角出现。", 13, Theme.Muted, FontWeights.Normal);
                copy.HorizontalAlignment = HorizontalAlignment.Center;
                empty.Children.Add(symbol);
                empty.Children.Add(heading);
                empty.Children.Add(copy);
                listPanel.Children.Add(empty);
                return;
            }

            List<Reminder> ordered = reminders.OrderByDescending(delegate(Reminder item) { return item.Enabled; })
                .ThenBy(delegate(Reminder item) { return item.NextTriggerAt ?? DateTime.MaxValue; }).ToList();
            foreach (Reminder reminder in ordered) listPanel.Children.Add(CreateReminderRow(reminder));
        }

        private UIElement CreateReminderRow(Reminder reminder)
        {
            Border row = new Border();
            row.BorderBrush = Theme.Line;
            row.BorderThickness = new Thickness(0, 1, 0, 0);
            row.Opacity = reminder.Enabled ? 1 : 0.62;

            Grid layout = new Grid();
            layout.MinHeight = 105;
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(126) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border timeRail = new Border();
            timeRail.Background = Theme.Brush("#F7F9F9");
            timeRail.BorderBrush = Theme.Line;
            timeRail.BorderThickness = new Thickness(0, 0, 1, 0);
            timeRail.Padding = new Thickness(24, 18, 16, 18);
            StackPanel timeStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            string timeMain;
            string timeSub;
            FormatNextTime(reminder, out timeMain, out timeSub);
            timeStack.Children.Add(Theme.Text(timeMain, reminder.Enabled ? 22 : 15, Theme.Green, FontWeights.Bold));
            TextBlock sub = Theme.Text(timeSub, 11, Theme.Muted, FontWeights.Normal);
            sub.Margin = new Thickness(0, 4, 0, 0);
            timeStack.Children.Add(sub);
            timeRail.Child = timeStack;
            Grid.SetColumn(timeRail, 0);
            layout.Children.Add(timeRail);

            StackPanel content = new StackPanel();
            content.Margin = new Thickness(24, 18, 18, 18);
            content.VerticalAlignment = VerticalAlignment.Center;
            TextBlock reminderTitle = Theme.Text(reminder.Title, 16, Theme.Ink, FontWeights.Bold);
            reminderTitle.TextTrimming = TextTrimming.CharacterEllipsis;
            reminderTitle.MaxWidth = 430;
            TextBlock schedule = Theme.Text(ScheduleSummary(reminder), 13, Theme.Muted, FontWeights.Normal);
            schedule.Margin = new Thickness(0, 7, 0, 0);
            content.Children.Add(reminderTitle);
            content.Children.Add(schedule);
            Grid.SetColumn(content, 1);
            layout.Children.Add(content);

            StackPanel actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.VerticalAlignment = VerticalAlignment.Center;
            actions.Margin = new Thickness(0, 14, 20, 14);

            CheckBox toggle = new CheckBox();
            toggle.IsChecked = reminder.Enabled;
            toggle.Content = reminder.Enabled ? "启用" : "暂停";
            toggle.Foreground = Theme.Green;
            toggle.FontWeight = FontWeights.SemiBold;
            toggle.VerticalAlignment = VerticalAlignment.Center;
            toggle.Margin = new Thickness(0, 0, 9, 0);
            toggle.Cursor = System.Windows.Input.Cursors.Hand;
            toggle.Checked += delegate { ToggleReminder(reminder, true); };
            toggle.Unchecked += delegate { ToggleReminder(reminder, false); };

            Button edit = Theme.Button("编辑", false);
            edit.Foreground = Theme.Green;
            edit.Background = System.Windows.Media.Brushes.Transparent;
            edit.Click += delegate { EditReminder(reminder); };
            Button delete = Theme.Button("删除", false);
            delete.Foreground = Theme.Danger;
            delete.Background = System.Windows.Media.Brushes.Transparent;
            delete.Click += delegate { DeleteReminder(reminder); };
            actions.Children.Add(toggle);
            actions.Children.Add(edit);
            actions.Children.Add(delete);
            Grid.SetColumn(actions, 2);
            layout.Children.Add(actions);

            row.Child = layout;
            return row;
        }

        private static string ScheduleSummary(Reminder reminder)
        {
            if (reminder.ScheduleType == "interval")
            {
                if (reminder.IntervalMinutes % 60 == 0)
                    return "每 " + (reminder.IntervalMinutes / 60) + " 小时";
                return "每 " + reminder.IntervalMinutes + " 分钟";
            }
            if (reminder.RepeatType == "daily") return "每天 " + reminder.Time;
            if (reminder.RepeatType == "weekdays") return "工作日 " + reminder.Time;
            string[] labels = new string[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            List<string> selected = new List<string>();
            foreach (int day in reminder.Weekdays) selected.Add(labels[day]);
            return string.Join("、", selected.ToArray()) + " " + reminder.Time;
        }

        private static void FormatNextTime(Reminder reminder, out string main, out string sub)
        {
            if (!reminder.Enabled || !reminder.NextTriggerAt.HasValue)
            {
                main = "已暂停";
                sub = "不会弹出提醒";
                return;
            }

            DateTime next = reminder.NextTriggerAt.Value;
            main = next.ToString("HH:mm");
            if (next.Date == DateTime.Today)
                sub = "今天";
            else if (next.Date == DateTime.Today.AddDays(1))
                sub = "明天";
            else
                sub = next.ToString("M月d日 ddd");
        }

        private bool TryCommit(List<Reminder> candidate)
        {
            try
            {
                store.Save(candidate);
                reminders = candidate;
                storageErrorShown = false;
                RenderReminders();
                return true;
            }
            catch (Exception error)
            {
                MessageBox.Show(this, "无法保存提醒：" + error.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
                RenderReminders();
                return false;
            }
        }

        private void ReconcileOverdue()
        {
            DateTime now = DateTime.Now;
            bool changed = false;
            List<Reminder> candidate = CloneReminders(reminders);
            foreach (Reminder reminder in candidate)
            {
                if (!reminder.Enabled) continue;
                if (!reminder.NextTriggerAt.HasValue || reminder.NextTriggerAt.Value <= now)
                {
                    reminder.NextTriggerAt = ReminderScheduler.AdvanceOverdue(reminder, now);
                    reminder.UpdatedAt = now;
                    changed = true;
                }
            }
            if (!changed) return;
            reminders = candidate;
            try
            {
                store.Save(candidate);
                storageErrorShown = false;
            }
            catch (Exception error)
            {
                ShowStorageErrorLater(error);
            }
        }

        private void CheckDueReminders()
        {
            DateTime now = DateTime.Now;
            bool changed = false;
            List<string> dueTitles = new List<string>();
            List<Reminder> candidate = CloneReminders(reminders);
            foreach (Reminder reminder in candidate)
            {
                if (!reminder.Enabled || !reminder.NextTriggerAt.HasValue || reminder.NextTriggerAt.Value > now) continue;
                if ((now - reminder.NextTriggerAt.Value).TotalSeconds <= 65)
                    dueTitles.Add(reminder.Title);
                reminder.NextTriggerAt = ReminderScheduler.AdvanceOverdue(reminder, now);
                reminder.UpdatedAt = now;
                changed = true;
            }
            if (!changed) return;

            reminders = candidate;
            try
            {
                store.Save(candidate);
                storageErrorShown = false;
            }
            catch (Exception error)
            {
                ShowStorageErrorLater(error);
            }
            RenderReminders();
            foreach (string title in dueTitles) new NotificationWindow(title).Show();
        }

        private void ShowStorageErrorLater(Exception error)
        {
            if (storageErrorShown) return;
            storageErrorShown = true;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                MessageBox.Show(
                    this,
                    "提醒仍会在本次运行中生效，但暂时无法写入本地文件：" + error.Message,
                    "保存失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }));
        }

        private static List<Reminder> CloneReminders(List<Reminder> source)
        {
            List<Reminder> result = new List<Reminder>();
            foreach (Reminder reminder in source)
            {
                result.Add(new Reminder
                {
                    Id = reminder.Id,
                    Title = reminder.Title,
                    ScheduleType = reminder.ScheduleType,
                    IntervalMinutes = reminder.IntervalMinutes,
                    Time = reminder.Time,
                    RepeatType = reminder.RepeatType,
                    Weekdays = reminder.Weekdays == null ? new List<int>() : new List<int>(reminder.Weekdays),
                    Enabled = reminder.Enabled,
                    NextTriggerAt = reminder.NextTriggerAt,
                    CreatedAt = reminder.CreatedAt,
                    UpdatedAt = reminder.UpdatedAt
                });
            }
            return result;
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void ExitApplication()
        {
            allowExit = true;
            schedulerTimer.Stop();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            Application.Current.Shutdown();
        }

        private void OnClosing(object sender, CancelEventArgs eventArgs)
        {
            if (allowExit) return;
            eventArgs.Cancel = true;
            Hide();
        }
    }
}
