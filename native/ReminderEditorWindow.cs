using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace LocalReminder
{
    internal sealed class ReminderEditorWindow : Window
    {
        private readonly Reminder original;
        private readonly TextBox titleInput;
        private readonly ComboBox scheduleTypeInput;
        private readonly TextBox intervalInput;
        private readonly ComboBox intervalUnitInput;
        private readonly TextBox timeInput;
        private readonly ComboBox repeatTypeInput;
        private readonly ComboBox popupModeInput;
        private readonly StackPanel intervalPanel;
        private readonly StackPanel fixedTimePanel;
        private readonly StackPanel weekdayPanel;
        private readonly List<CheckBox> weekdayChecks = new List<CheckBox>();
        private readonly TextBlock errorText;

        public Reminder ResultReminder { get; private set; }

        public ReminderEditorWindow(Reminder reminder)
        {
            original = reminder;
            Title = reminder == null ? "新建提醒" : "编辑提醒";
            Width = 560;
            Height = 610;
            MinWidth = 520;
            MinHeight = 570;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = Theme.Canvas;
            FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");

            Grid root = new Grid();
            root.Margin = new Thickness(26);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel heading = new StackPanel();
            TextBlock eyebrow = Theme.Text(reminder == null ? "新提醒" : "编辑提醒", 11, Theme.Green, FontWeights.Bold);
            eyebrow.Margin = new Thickness(0, 0, 0, 5);
            heading.Children.Add(eyebrow);
            heading.Children.Add(Theme.Text(reminder == null ? "添加提醒事项" : "修改提醒事项", 22, Theme.Ink, FontWeights.Bold));
            Grid.SetRow(heading, 0);
            root.Children.Add(heading);

            StackPanel fields = new StackPanel();
            fields.Margin = new Thickness(0, 24, 0, 12);

            titleInput = CreateTextBox("例如：站起来活动一下");
            titleInput.MaxLength = 80;
            AddField(fields, "提醒内容", titleInput);

            popupModeInput = CreateComboBox(new string[] { "右上角", "全屏" });
            AddField(fields, "弹窗方式", popupModeInput);

            scheduleTypeInput = CreateComboBox(new string[] { "固定间隔", "固定时间", "连续亮屏" });
            AddField(fields, "提醒方式", scheduleTypeInput);

            intervalPanel = new StackPanel();
            Grid intervalGrid = new Grid();
            intervalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            intervalGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            intervalInput = CreateTextBox(string.Empty);
            intervalInput.Text = "30";
            intervalUnitInput = CreateComboBox(new string[] { "分钟", "小时" });
            intervalUnitInput.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(intervalUnitInput, 1);
            intervalGrid.Children.Add(intervalInput);
            intervalGrid.Children.Add(intervalUnitInput);
            AddField(intervalPanel, "提醒时长", intervalGrid);
            fields.Children.Add(intervalPanel);

            fixedTimePanel = new StackPanel();
            timeInput = CreateTextBox("HH:mm，例如 09:30");
            timeInput.Text = "09:00";
            AddField(fixedTimePanel, "提醒时间", timeInput);
            repeatTypeInput = CreateComboBox(new string[] { "每天", "仅工作日", "自定义星期" });
            AddField(fixedTimePanel, "重复日期", repeatTypeInput);

            weekdayPanel = new StackPanel();
            TextBlock weekdayLabel = Theme.Text("选择星期", 12, Theme.Ink, FontWeights.SemiBold);
            weekdayLabel.Margin = new Thickness(0, 0, 0, 8);
            weekdayPanel.Children.Add(weekdayLabel);
            UniformGrid weekdayGrid = new UniformGrid { Columns = 7 };
            int[] values = new int[] { 1, 2, 3, 4, 5, 6, 0 };
            string[] labels = new string[] { "一", "二", "三", "四", "五", "六", "日" };
            for (int index = 0; index < values.Length; index++)
            {
                CheckBox check = new CheckBox();
                check.Content = labels[index];
                check.Tag = values[index];
                check.HorizontalContentAlignment = HorizontalAlignment.Center;
                check.Margin = new Thickness(2);
                check.Padding = new Thickness(7);
                weekdayChecks.Add(check);
                weekdayGrid.Children.Add(check);
            }
            weekdayPanel.Children.Add(weekdayGrid);
            fixedTimePanel.Children.Add(weekdayPanel);
            fields.Children.Add(fixedTimePanel);

            errorText = Theme.Text(string.Empty, 12, Theme.Danger, FontWeights.SemiBold);
            errorText.MinHeight = 18;
            errorText.Margin = new Thickness(0, 10, 0, 0);
            fields.Children.Add(errorText);

            ScrollViewer scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scroll.Content = fields;
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button cancel = Theme.Button("取消", false);
            Button save = Theme.Button("保存提醒", true);
            cancel.Click += delegate { DialogResult = false; };
            save.Click += SaveClick;
            actions.Children.Add(cancel);
            actions.Children.Add(save);
            Grid.SetRow(actions, 2);
            root.Children.Add(actions);

            Content = root;

            scheduleTypeInput.SelectionChanged += delegate { UpdateVisibility(); };
            repeatTypeInput.SelectionChanged += delegate { UpdateVisibility(); };
            LoadReminder();
            UpdateVisibility();
        }

        private static TextBox CreateTextBox(string hint)
        {
            TextBox input = new TextBox();
            input.Height = 40;
            input.Padding = new Thickness(10, 8, 10, 8);
            input.BorderBrush = Theme.Line;
            input.Background = System.Windows.Media.Brushes.White;
            input.ToolTip = hint;
            return input;
        }

        private static ComboBox CreateComboBox(string[] items)
        {
            ComboBox input = new ComboBox();
            input.Height = 40;
            input.Padding = new Thickness(8, 5, 8, 5);
            input.BorderBrush = Theme.Line;
            input.Background = System.Windows.Media.Brushes.White;
            foreach (string item in items) input.Items.Add(item);
            input.SelectedIndex = 0;
            return input;
        }

        private static void AddField(Panel parent, string label, UIElement input)
        {
            StackPanel field = new StackPanel();
            field.Margin = new Thickness(0, 0, 0, 16);
            TextBlock labelBlock = Theme.Text(label, 12, Theme.Ink, FontWeights.SemiBold);
            labelBlock.Margin = new Thickness(0, 0, 0, 7);
            field.Children.Add(labelBlock);
            field.Children.Add(input);
            parent.Children.Add(field);
        }

        private void LoadReminder()
        {
            if (original == null) return;
            titleInput.Text = original.Title;
            popupModeInput.SelectedIndex = original.PopupMode == "fullscreen" ? 1 : 0;
            scheduleTypeInput.SelectedIndex = original.ScheduleType == "fixedTime" ? 1 : original.ScheduleType == "screenTime" ? 2 : 0;
            if (original.ScheduleType == "interval" || original.ScheduleType == "screenTime")
            {
                bool hours = original.IntervalMinutes >= 60 && original.IntervalMinutes % 60 == 0;
                intervalUnitInput.SelectedIndex = hours ? 1 : 0;
                intervalInput.Text = (hours ? original.IntervalMinutes / 60 : original.IntervalMinutes).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                timeInput.Text = original.Time;
                repeatTypeInput.SelectedIndex = original.RepeatType == "weekdays" ? 1 : original.RepeatType == "custom" ? 2 : 0;
                foreach (CheckBox check in weekdayChecks)
                    check.IsChecked = original.Weekdays != null && original.Weekdays.Contains((int)check.Tag);
            }
        }

        private void UpdateVisibility()
        {
            bool fixedTime = scheduleTypeInput.SelectedIndex == 1;
            intervalPanel.Visibility = fixedTime ? Visibility.Collapsed : Visibility.Visible;
            fixedTimePanel.Visibility = fixedTime ? Visibility.Visible : Visibility.Collapsed;
            weekdayPanel.Visibility = fixedTime && repeatTypeInput.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SaveClick(object sender, RoutedEventArgs eventArgs)
        {
            try
            {
                Reminder reminder = new Reminder();
                reminder.Id = original == null ? Guid.NewGuid().ToString("N") : original.Id;
                reminder.Title = titleInput.Text;
                reminder.PopupMode = popupModeInput.SelectedIndex == 1 ? "fullscreen" : "corner";
                reminder.Enabled = original == null || original.Enabled;
                reminder.CreatedAt = original == null ? DateTime.Now : original.CreatedAt;
                reminder.UpdatedAt = DateTime.Now;

                if (scheduleTypeInput.SelectedIndex != 1)
                {
                    int value;
                    if (!int.TryParse(intervalInput.Text, out value)) throw new ArgumentException("请输入整数提醒间隔");
                    checked { reminder.IntervalMinutes = intervalUnitInput.SelectedIndex == 1 ? value * 60 : value; }
                    reminder.ScheduleType = scheduleTypeInput.SelectedIndex == 2 ? "screenTime" : "interval";
                }
                else
                {
                    reminder.ScheduleType = "fixedTime";
                    reminder.Time = timeInput.Text.Trim();
                    reminder.RepeatType = repeatTypeInput.SelectedIndex == 1 ? "weekdays" : repeatTypeInput.SelectedIndex == 2 ? "custom" : "daily";
                    foreach (CheckBox check in weekdayChecks)
                        if (check.IsChecked == true) reminder.Weekdays.Add((int)check.Tag);
                }

                ReminderValidator.Validate(reminder);
                reminder.NextTriggerAt = reminder.Enabled && reminder.ScheduleType != "screenTime"
                    ? (DateTime?)ReminderScheduler.ComputeNext(reminder, DateTime.Now)
                    : null;
                ResultReminder = reminder;
                DialogResult = true;
            }
            catch (OverflowException)
            {
                errorText.Text = "提醒间隔数值过大";
            }
            catch (ArgumentException error)
            {
                errorText.Text = error.Message;
            }
        }
    }
}
