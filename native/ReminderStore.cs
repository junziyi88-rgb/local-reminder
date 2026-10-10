using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;

namespace LocalReminder
{
    public sealed class ReminderStore
    {
        private readonly string filePath;

        public ReminderStore(string filePath)
        {
            this.filePath = filePath;
        }

        public List<Reminder> Load()
        {
            if (!File.Exists(filePath)) return new List<Reminder>();
            try
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<Reminder>));
                using (FileStream stream = File.OpenRead(filePath))
                {
                    object value = serializer.ReadObject(stream);
                    List<Reminder> reminders = value as List<Reminder>;
                    if (reminders == null) throw new InvalidDataException("提醒数据格式无效");
                    HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
                    foreach (Reminder reminder in reminders)
                    {
                        if (reminder == null) throw new InvalidDataException("提醒数据包含空项目");
                        if (string.IsNullOrWhiteSpace(reminder.Id)) throw new InvalidDataException("提醒缺少标识");
                        if (!ids.Add(reminder.Id)) throw new InvalidDataException("提醒标识重复");
                        ReminderValidator.Validate(reminder);
                        if (reminder.CreatedAt == DateTime.MinValue) reminder.CreatedAt = DateTime.Now;
                        if (reminder.UpdatedAt == DateTime.MinValue) reminder.UpdatedAt = reminder.CreatedAt;
                    }
                    return reminders;
                }
            }
            catch
            {
                string directory = Path.GetDirectoryName(filePath);
                string baseName = Path.GetFileNameWithoutExtension(filePath);
                string backupName = baseName + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".json";
                File.Move(filePath, Path.Combine(directory, backupName));
                return new List<Reminder>();
            }
        }

        public void Save(List<Reminder> reminders)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Reminder reminder in reminders)
            {
                if (reminder == null || string.IsNullOrWhiteSpace(reminder.Id))
                    throw new InvalidDataException("提醒数据无效");
                if (!ids.Add(reminder.Id)) throw new InvalidDataException("提醒标识重复");
                ReminderValidator.Validate(reminder);
            }
            string directory = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directory);
            string temporaryPath = filePath + ".tmp";
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<Reminder>));
            using (FileStream stream = File.Create(temporaryPath))
            {
                serializer.WriteObject(stream, reminders);
            }

            if (File.Exists(filePath))
                File.Replace(temporaryPath, filePath, null);
            else
                File.Move(temporaryPath, filePath);
        }
    }
}
