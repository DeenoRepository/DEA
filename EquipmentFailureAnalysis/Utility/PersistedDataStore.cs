using EquipmentFailureAnalysis.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace EquipmentFailureAnalysis.Utility
{
    public static class PersistedDataStore
    {
        private sealed class PersistedIssue
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public string Description { get; set; } = string.Empty;
            public IssueType Type { get; set; }
            public string? Responsible { get; set; }
            public bool IsInProgress { get; set; }
            public string? JiraIssueKey { get; set; }
            public string? Reporter { get; set; }
            public string? Comments { get; set; }
        }

        private sealed class PersistedEquipment
        {
            public int Uid { get; set; }
            public string Title { get; set; } = string.Empty;
            public string? InventoryNumber { get; set; }
            public string? Subdivision { get; set; }
            public List<PersistedIssue> Issues { get; set; } = new List<PersistedIssue>();
        }

        private static string GetStorageDirectory()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EquipmentFailureAnalysis");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        private static string GetJiraCacheFilePath()
        {
            return Path.Combine(GetStorageDirectory(), "jira_import_cache.json");
        }

        public static void SaveJiraImportedEquipment(IEnumerable<EquipmentInfo> equipment)
        {
            var payload = (equipment ?? Enumerable.Empty<EquipmentInfo>())
                .Select(e => new PersistedEquipment
                {
                    Uid = e.Uid,
                    Title = e.Title ?? string.Empty,
                    InventoryNumber = e.InventoryNumber,
                    Subdivision = e.Subdivision,
                    Issues = (e.Issues ?? new ObservableCollection<Issue>())
                        .Select(i => new PersistedIssue
                        {
                            Start = i.Start,
                            End = i.End,
                            Description = i.Description ?? string.Empty,
                            Type = i.Type,
                            Responsible = i.Responsible,
                            IsInProgress = i.IsInProgress,
                            JiraIssueKey = i.JiraIssueKey,
                            Reporter = i.Reporter,
                            Comments = i.Comments
                        })
                        .ToList()
                })
                .ToList();

            var json = JsonSerializer.Serialize(payload);
            File.WriteAllText(GetJiraCacheFilePath(), json);
        }

        public static bool TryLoadJiraImportedEquipment(out ObservableCollection<EquipmentInfo> equipment)
        {
            equipment = new ObservableCollection<EquipmentInfo>();

            var file = GetJiraCacheFilePath();
            if (!File.Exists(file))
                return false;

            try
            {
                var json = File.ReadAllText(file);
                var payload = JsonSerializer.Deserialize<List<PersistedEquipment>>(json) ?? new List<PersistedEquipment>();

                foreach (var item in payload)
                {
                    var eq = new EquipmentInfo
                    {
                        Uid = item.Uid,
                        Title = item.Title ?? string.Empty,
                        InventoryNumber = item.InventoryNumber,
                        Subdivision = item.Subdivision
                    };

                    foreach (var issue in item.Issues ?? new List<PersistedIssue>())
                    {
                        eq.Issues.Add(new Issue
                        {
                            Start = issue.Start,
                            End = issue.End,
                            Description = issue.Description ?? string.Empty,
                            Type = issue.Type,
                            Responsible = issue.Responsible,
                            IsInProgress = issue.IsInProgress,
                            JiraIssueKey = issue.JiraIssueKey,
                            Reporter = issue.Reporter,
                            Comments = issue.Comments
                        });
                    }

                    equipment.Add(eq);
                }

                return equipment.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public static void SaveTheme(string theme)
        {
            try
            {
                var file = Path.Combine(GetStorageDirectory(), "theme_setting.txt");
                File.WriteAllText(file, theme ?? "Light");
            }
            catch { }
        }

        public static string LoadTheme()
        {
            try
            {
                var file = Path.Combine(GetStorageDirectory(), "theme_setting.txt");
                if (File.Exists(file))
                {
                    return File.ReadAllText(file).Trim();
                }
            }
            catch { }
            return "Light";
        }
    }
}
