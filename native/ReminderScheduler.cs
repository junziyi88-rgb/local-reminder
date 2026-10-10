using System;
using System.Globalization;

namespace LocalReminder
{
    public static class ReminderScheduler
    {
        public static DateTime ComputeNext(Reminder reminder, DateTime from)
        {
            if (reminder.ScheduleType == "interval")
            {
                if (reminder.IntervalMinutes < 1) throw new ArgumentException("提醒间隔必须是正整数");
                return from.AddMinutes(reminder.IntervalMinutes);
            }

            DateTime parsed;
            if (!DateTime.TryParseExact(reminder.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                throw new ArgumentException("提醒时间无效");

            for (int offset = 0; offset <= 7; offset++)
            {
                DateTime day = from.Date.AddDays(offset);
                DateTime candidate = day.AddHours(parsed.Hour).AddMinutes(parsed.Minute);
                if (candidate > from && MatchesDay(reminder, candidate)) return candidate;
            }

            throw new ArgumentException("无法计算下一次提醒时间");
        }

        public static DateTime AdvanceOverdue(Reminder reminder, DateTime now)
        {
            if (reminder.ScheduleType != "interval") return ComputeNext(reminder, now);
            if (!reminder.NextTriggerAt.HasValue || reminder.NextTriggerAt.Value > now)
                return ComputeNext(reminder, now);

            double intervalTicks = TimeSpan.FromMinutes(reminder.IntervalMinutes).Ticks;
            long elapsedIntervals = (long)Math.Floor((now.Ticks - reminder.NextTriggerAt.Value.Ticks) / intervalTicks) + 1;
            return reminder.NextTriggerAt.Value.AddMinutes(elapsedIntervals * reminder.IntervalMinutes);
        }

        private static bool MatchesDay(Reminder reminder, DateTime date)
        {
            if (reminder.RepeatType == "daily") return true;
            if (reminder.RepeatType == "weekdays")
                return date.DayOfWeek >= DayOfWeek.Monday && date.DayOfWeek <= DayOfWeek.Friday;
            if (reminder.RepeatType == "custom")
                return reminder.Weekdays != null && reminder.Weekdays.Contains((int)date.DayOfWeek);
            return false;
        }
    }
}
