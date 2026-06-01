using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace EquipmentFailureAnalysis.Services
{
    public sealed class XmlImportResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public ObservableCollection<EquipmentInfo> Items { get; set; } = new ObservableCollection<EquipmentInfo>();
    }

    public sealed class XmlImportService
    {
        private readonly string _storageDirectory;

        public XmlImportService(string appFolderName = "EquipmentFailureAnalysis")
        {
            _storageDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                appFolderName);
        }

        public XmlImportResult ImportFromPaths(IEnumerable<string> paths, bool persistFirstPath = true)
        {
            var normalized = (paths ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (normalized.Length == 0)
            {
                return new XmlImportResult
                {
                    Success = false,
                    ErrorMessage = "Не выбраны файлы XML для импорта."
                };
            }

            try
            {
                var decoder = new XmlDataDecoder(normalized);
                var items = decoder.DecodeEquipment();

                if (persistFirstPath)
                    TrySaveLastImportedPath(normalized[0]);

                try
                {
                    // Persist a full snapshot so the latest XML import is restored on next app start.
                    PersistedDataStore.SaveJiraImportedEquipment(items);
                }
                catch
                {
                    // ignore cache persistence errors
                }

                return new XmlImportResult
                {
                    Success = true,
                    Items = items
                };
            }
            catch (Exception ex)
            {
                return new XmlImportResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public bool TryLoadLastImportedEquipment(out ObservableCollection<EquipmentInfo> items)
        {
            items = new ObservableCollection<EquipmentInfo>();

            var path = TryLoadLastImportedPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            try
            {
                var decoder = new XmlDataDecoder(path);
                items = decoder.DecodeEquipment();
                return items.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private void TrySaveLastImportedPath(string path)
        {
            try
            {
                Directory.CreateDirectory(_storageDirectory);
                File.WriteAllText(GetLastImportedPathFile(), path);
            }
            catch
            {
                // ignore persistence errors
            }
        }

        private string? TryLoadLastImportedPath()
        {
            var file = GetLastImportedPathFile();
            if (!File.Exists(file))
                return null;

            try
            {
                var path = File.ReadAllText(file).Trim();
                return string.IsNullOrWhiteSpace(path) ? null : path;
            }
            catch
            {
                return null;
            }
        }

        private string GetLastImportedPathFile()
            => Path.Combine(_storageDirectory, "last_imported_xml.txt");

        public async Task<XmlImportResult> ImportFromPathsAsync(IEnumerable<string> paths, bool persistFirstPath = true)
        {
            return await Task.Run(() => ImportFromPaths(paths, persistFirstPath));
        }

        public async Task<(bool Success, ObservableCollection<EquipmentInfo> Items)> TryLoadLastImportedEquipmentAsync()
        {
            return await Task.Run(() =>
            {
                var success = TryLoadLastImportedEquipment(out var items);
                return (success, items);
            });
        }
    }
}
