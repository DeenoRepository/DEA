using System;

namespace EquipmentFailureAnalysis.Models
{
    public class DataWarning
    {
        public string Title { get; set; } = string.Empty;
        public string Severity { get; set; } = "Warning"; // Warning or Error
        public string Description { get; set; } = string.Empty;
        public string EquipmentTitle { get; set; } = string.Empty;
        public string JiraIssueKey { get; set; } = string.Empty;
        public DateTime Start { get; set; }
    }
}
