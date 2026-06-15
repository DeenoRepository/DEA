using System;

namespace EquipmentFailureAnalysis.Models
{
    public class Annotation
    {
        // hour in day (0..24)
        public double Hour { get; set; }
        // overlap start hour in day (0..24)
        public double StartHour { get; set; }
        // overlap end hour in day (0..24)
        public double EndHour { get; set; }
        // description of the issue
        public string Description { get; set; } = string.Empty;
        // responsible person
        public string Responsible { get; set; } = string.Empty;
        // overlap start date/time
        public DateTime StartDate { get; set; }
        // overlap end date/time
        public DateTime EndDate { get; set; }

        public string TimeInterval => $"{StartDate:HH:mm} - {EndDate:HH:mm}";
        // duration string like HH:mm
        public string Duration { get; set; } = string.Empty;
        // issue type (Настройка or Ремонт)
        public IssueType Type { get; set; }
        // issue key from source system (for example Jira), if available
        public string JiraIssueKey { get; set; } = string.Empty;
        // creator/reporter of the task
        public string Reporter { get; set; } = string.Empty;
        // comments text from the task
        public string Comments { get; set; } = string.Empty;
        // marker that the task is still in progress
        public bool IsInProgress { get; set; }

        public bool IsRepair => Type == IssueType.Ремонт;
        public bool IsSetup => Type == IssueType.Настройка;
    }
}
