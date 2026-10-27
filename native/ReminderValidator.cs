using System;
using System.Collections.Generic;
using System.Globalization;

namespace LocalReminder
{
    public static class ReminderValidator
    {
        public static void Validate(Reminder reminder)
        {
            if (reminder == null) throw new ArgumentException("提醒内容无效");
            reminder.Title = (reminder.Title ?? string.Empty).Trim();
            if (reminder.Title.Length == 0) throw new ArgumentException("请输入提醒内容");

            if (string.IsNullOrWhiteSpace(reminder.PopupMode)) reminder.PopupMode = "corner";
            if (reminder.PopupMode != "corner" && reminder.PopupMode != "fullscreen")
                throw new ArgumentException("请选择弹窗方式");

            if (reminder.ScheduleType == "interval" || reminder.ScheduleType == "screenTime")
            {
                if (reminder.IntervalMinutes < 1 || reminder.IntervalMinutes > 525600)
                    throw new ArgumentException("提醒间隔必须在 1 到 525600 分钟之间");
                return;
            }

            if (reminder.ScheduleType != "fixedTime") throw new ArgumentException("请选择提醒方式");

            DateTime parsed;
            if (!DateTime.TryParseExact(reminder.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                throw new ArgumentException("请输入 HH:mm 格式的提醒时间");

            if (reminder.RepeatType != "daily" && reminder.RepeatType != "weekdays" && reminder.RepeatType != "custom")
                throw new ArgumentException("请选择重复日期");

            if (reminder.RepeatType == "custom")
            {
                if (reminder.Weekdays == null || reminder.Weekdays.Count == 0)
                    throw new ArgumentException("自定义重复至少选择一天");
                HashSet<int> uniqueDays = new HashSet<int>();
                foreach (int day in reminder.Weekdays)
                {
                    if (day < 0 || day > 6) throw new ArgumentException("星期设置无效");
                    uniqueDays.Add(day);
                }
                reminder.Weekdays = new List<int>(uniqueDays);
                reminder.Weekdays.Sort();
            }
        }
    }
}
