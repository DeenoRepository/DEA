using System;
using System.IO;
using System.Text.Json;

namespace EquipmentFailureAnalysis.Services
{
    public sealed class JiraSettingsStore
    {
        private readonly string _storageDirectory;

        public JiraSettingsStore(string appFolderName = "EquipmentFailureAnalysis")
        {
            _storageDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                appFolderName);
        }

        public void Save<T>(string fileName, T payload)
        {
            Directory.CreateDirectory(_storageDirectory);
            var filePath = Path.Combine(_storageDirectory, fileName);
            var json = JsonSerializer.Serialize(payload);
            File.WriteAllText(filePath, json);
        }

        public bool TryLoad<T>(string fileName, out T? payload)
        {
            payload = default;
            var filePath = Path.Combine(_storageDirectory, fileName);
            if (!File.Exists(filePath))
                return false;

            try
            {
                var json = File.ReadAllText(filePath);
                payload = JsonSerializer.Deserialize<T>(json);
                return payload != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
