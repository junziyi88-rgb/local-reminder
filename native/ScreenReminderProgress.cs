using System;
using System.Collections.Generic;

namespace LocalReminder
{
    internal sealed class ScreenReminderProgress
    {
        private sealed class ReminderState
        {
            internal int IntervalMinutes;
            internal double NextTriggerMinutes;
        }

        private readonly Dictionary<string, ReminderState> states = new Dictionary<string, ReminderState>();
        private long sessionId = long.MinValue;

        internal bool IsDue(Reminder reminder, TimeSpan usage, long currentSessionId)
        {
            if (sessionId != currentSessionId)
            {
                sessionId = currentSessionId;
                states.Clear();
            }

            if (reminder == null || !reminder.Enabled || reminder.ScheduleType != "screenTime") return false;
            ReminderState state;
            if (!states.TryGetValue(reminder.Id, out state))
            {
                state = new ReminderState
                {
                    IntervalMinutes = reminder.IntervalMinutes,
                    NextTriggerMinutes = reminder.IntervalMinutes
                };
                states[reminder.Id] = state;
            }
            else if (state.IntervalMinutes != reminder.IntervalMinutes)
            {
                state.IntervalMinutes = reminder.IntervalMinutes;
                state.NextTriggerMinutes = usage.TotalMinutes + reminder.IntervalMinutes;
                return false;
            }

            if (usage.TotalMinutes < state.NextTriggerMinutes) return false;

            do
            {
                state.NextTriggerMinutes += reminder.IntervalMinutes;
            }
            while (state.NextTriggerMinutes <= usage.TotalMinutes);
            return true;
        }

        internal void Reset(Reminder reminder, TimeSpan usage, long currentSessionId)
        {
            if (sessionId != currentSessionId)
            {
                sessionId = currentSessionId;
                states.Clear();
            }

            if (reminder == null) return;
            if (!reminder.Enabled || reminder.ScheduleType != "screenTime")
            {
                states.Remove(reminder.Id);
                return;
            }

            states[reminder.Id] = new ReminderState
            {
                IntervalMinutes = reminder.IntervalMinutes,
                NextTriggerMinutes = usage.TotalMinutes + reminder.IntervalMinutes
            };
        }

        internal void Remove(string reminderId)
        {
            states.Remove(reminderId);
        }
    }
}
