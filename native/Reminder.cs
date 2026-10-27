using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace LocalReminder
{
    [DataContract]
    public sealed class Reminder
    {
        public Reminder()
        {
            Weekdays = new List<int>();
            PopupMode = "corner";
            CreatedAt = DateTime.Now;
            UpdatedAt = DateTime.Now;
        }

        [DataMember] public string Id { get; set; }
        [DataMember] public string Title { get; set; }
        [DataMember] public string ScheduleType { get; set; }
        [DataMember] public int IntervalMinutes { get; set; }
        [DataMember] public string Time { get; set; }
        [DataMember] public string RepeatType { get; set; }
        [DataMember] public List<int> Weekdays { get; set; }
        [DataMember] public bool Enabled { get; set; }
        [DataMember] public string PopupMode { get; set; }
        [DataMember] public DateTime? NextTriggerAt { get; set; }
        [DataMember] public DateTime CreatedAt { get; set; }
        [DataMember] public DateTime UpdatedAt { get; set; }
    }
}
