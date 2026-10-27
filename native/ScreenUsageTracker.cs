using System;

namespace LocalReminder
{
    internal sealed class ScreenUsageTracker
    {
        private static readonly TimeSpan ResetAfter = TimeSpan.FromMinutes(5);
        private TimeSpan lastUpdatedAt;
        private TimeSpan? screenOffAt;
        private TimeSpan accumulatedUsage;
        private bool screenOn = true;
        private bool breakApplied;

        internal ScreenUsageTracker(TimeSpan now)
        {
            lastUpdatedAt = now;
        }

        internal long SessionId { get; private set; }
        internal bool IsScreenOn { get { return screenOn; } }

        internal void SetScreenOn(bool isOn, TimeSpan now)
        {
            Advance(now);
            if (screenOn == isOn) return;

            screenOn = isOn;
            if (isOn)
            {
                screenOffAt = null;
                breakApplied = false;
            }
            else
            {
                screenOffAt = now;
                breakApplied = false;
            }
        }

        internal TimeSpan GetUsage(TimeSpan now)
        {
            Advance(now);
            return accumulatedUsage;
        }

        private void Advance(TimeSpan now)
        {
            if (now < lastUpdatedAt)
            {
                lastUpdatedAt = now;
                return;
            }

            if (screenOn)
            {
                accumulatedUsage += now - lastUpdatedAt;
            }
            else if (!breakApplied && screenOffAt.HasValue && now - screenOffAt.Value >= ResetAfter)
            {
                accumulatedUsage = TimeSpan.Zero;
                SessionId++;
                breakApplied = true;
            }

            lastUpdatedAt = now;
        }
    }
}
