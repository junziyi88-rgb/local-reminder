using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using LocalReminder;

internal static class SchedulerTests
{
    private static int failures;

    private static void Equal(DateTime expected, DateTime actual, string name)
    {
        if (expected != actual)
        {
            failures++;
            Console.WriteLine("FAIL {0}: expected {1:o}, actual {2:o}", name, expected, actual);
        }
        else
        {
            Console.WriteLine("PASS {0}", name);
        }
    }

    private static void Throws(Action action, string name)
    {
        try
        {
            action();
            failures++;
            Console.WriteLine("FAIL {0}: expected exception", name);
        }
        catch (ArgumentException)
        {
            Console.WriteLine("PASS {0}", name);
        }
    }

    public static int Main()
    {
        DateTime now = new DateTime(2026, 10, 10, 8, 0, 0);
        Equal(
            new DateTime(2026, 10, 10, 8, 30, 0),
            ReminderScheduler.ComputeNext(new Reminder { ScheduleType = "interval", IntervalMinutes = 30 }, now),
            "interval from now");

        Equal(
            new DateTime(2026, 10, 10, 9, 15, 0),
            ReminderScheduler.ComputeNext(new Reminder { ScheduleType = "fixedTime", Time = "09:15", RepeatType = "daily" }, now),
            "daily today");

        Equal(
            new DateTime(2026, 10, 11, 9, 15, 0),
            ReminderScheduler.ComputeNext(new Reminder { ScheduleType = "fixedTime", Time = "09:15", RepeatType = "daily" }, new DateTime(2026, 10, 10, 10, 0, 0)),
            "daily tomorrow");

        Equal(
            new DateTime(2026, 10, 12, 9, 0, 0),
            ReminderScheduler.ComputeNext(new Reminder { ScheduleType = "fixedTime", Time = "09:00", RepeatType = "weekdays" }, new DateTime(2026, 10, 9, 18, 0, 0)),
            "weekday skips weekend");

        Equal(
            new DateTime(2026, 10, 7, 7, 30, 0),
            ReminderScheduler.ComputeNext(new Reminder
            {
                ScheduleType = "fixedTime",
                Time = "07:30",
                RepeatType = "custom",
                Weekdays = new List<int> { 1, 3, 5 }
            }, new DateTime(2026, 10, 6, 18, 0, 0)),
            "custom weekdays");

        Equal(
            new DateTime(2026, 10, 10, 10, 30, 0),
            ReminderScheduler.AdvanceOverdue(new Reminder
            {
                ScheduleType = "interval",
                IntervalMinutes = 30,
                NextTriggerAt = new DateTime(2026, 10, 10, 8, 0, 0)
            }, new DateTime(2026, 10, 10, 10, 10, 0)),
            "skip missed intervals");

        Throws(delegate
        {
            ReminderValidator.Validate(new Reminder { Title = " ", ScheduleType = "interval", IntervalMinutes = 15 });
        }, "empty title");

        Throws(delegate
        {
            ReminderValidator.Validate(new Reminder
            {
                Title = "活动",
                ScheduleType = "fixedTime",
                Time = "09:00",
                RepeatType = "custom",
                Weekdays = new List<int>()
            });
        }, "empty custom weekdays");

        string storeDirectory = Path.Combine(Path.GetTempPath(), "local-reminder-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storeDirectory);
        string storePath = Path.Combine(storeDirectory, "reminders.json");
        ReminderStore store = new ReminderStore(storePath);
        List<Reminder> loaded;
        try
        {
        store.Save(new List<Reminder> { new Reminder { Id = "1", Title = "喝水", Enabled = true, ScheduleType = "interval", IntervalMinutes = 30 } });
            loaded = store.Load();
        }
        catch (Exception error)
        {
            Console.WriteLine("STORE_ERROR_TYPE {0}", error.GetType().FullName);
            Console.WriteLine("STORE_ERROR_MESSAGE {0}", error.Message);
            return 1;
        }
        if (loaded.Count == 1 && loaded[0].Title == "喝水")
            Console.WriteLine("PASS store roundtrip");
        else
        {
            failures++;
            Console.WriteLine("FAIL store roundtrip");
        }

        File.WriteAllText(storePath, "{broken");
        loaded = store.Load();
        bool backupExists = Directory.GetFiles(storeDirectory, "reminders.corrupt-*.json").Length == 1;
        if (loaded.Count == 0 && backupExists)
            Console.WriteLine("PASS corrupt store backup");
        else
        {
            failures++;
            Console.WriteLine("FAIL corrupt store backup");
        }

        string semanticPath = Path.Combine(storeDirectory, "semantic.json");
        File.WriteAllText(semanticPath, "[null]");
        ReminderStore semanticStore = new ReminderStore(semanticPath);
        loaded = semanticStore.Load();
        bool semanticBackup = Directory.GetFiles(storeDirectory, "semantic.corrupt-*.json").Length == 1;
        if (loaded.Count == 0 && semanticBackup)
            Console.WriteLine("PASS null reminder is quarantined");
        else
        {
            failures++;
            Console.WriteLine("FAIL null reminder is quarantined");
        }

        string weekdayPath = Path.Combine(storeDirectory, "weekday.json");
        DataContractJsonSerializer rawSerializer = new DataContractJsonSerializer(typeof(List<Reminder>));
        using (FileStream rawStream = File.Create(weekdayPath))
        {
            rawSerializer.WriteObject(rawStream, new List<Reminder>
            {
                new Reminder
                {
                    Id = "bad-day",
                    Title = "无效星期",
                    Enabled = true,
                    ScheduleType = "fixedTime",
                    Time = "09:00",
                    RepeatType = "custom",
                    Weekdays = new List<int> { 7 }
                }
            });
        }
        ReminderStore weekdayStore = new ReminderStore(weekdayPath);
        loaded = weekdayStore.Load();
        bool weekdayBackup = Directory.GetFiles(storeDirectory, "weekday.corrupt-*.json").Length == 1;
        if (loaded.Count == 0 && weekdayBackup)
            Console.WriteLine("PASS invalid weekday is quarantined");
        else
        {
            failures++;
            Console.WriteLine("FAIL invalid weekday is quarantined");
        }

        string duplicatePath = Path.Combine(storeDirectory, "duplicate.json");
        using (FileStream rawStream = File.Create(duplicatePath))
        {
            rawSerializer.WriteObject(rawStream, new List<Reminder>
            {
                new Reminder { Id = "same", Title = "提醒一", Enabled = true, ScheduleType = "interval", IntervalMinutes = 30 },
                new Reminder { Id = "same", Title = "提醒二", Enabled = true, ScheduleType = "interval", IntervalMinutes = 60 }
            });
        }
        ReminderStore duplicateStore = new ReminderStore(duplicatePath);
        loaded = duplicateStore.Load();
        bool duplicateBackup = Directory.GetFiles(storeDirectory, "duplicate.corrupt-*.json").Length == 1;
        if (loaded.Count == 0 && duplicateBackup)
            Console.WriteLine("PASS duplicate id is quarantined");
        else
        {
            failures++;
            Console.WriteLine("FAIL duplicate id is quarantined");
        }
        Directory.Delete(storeDirectory, true);

        Console.WriteLine(failures == 0 ? "ALL_PASS" : "FAILED");
        return failures == 0 ? 0 : 1;
    }
}
