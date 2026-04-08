using System;

namespace EquipmentFailureAnalysis.Models
{
    public class EmployeeAnalysisRow
    {
        public string Name { get; set; } = "Не назначен";
        public int IssuesCount { get; set; }
        public double EventSharePercent { get; set; }
        public int RepairsCount { get; set; }
        public int SetupsCount { get; set; }
        public double RepairsSharePercent { get; set; }
        public int SlaMetCount { get; set; }
        public double SlaCompliancePercent { get; set; }
        public int EquipmentCount { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public string TotalDurationText { get; set; } = "00:00";
        public double AvgDurationMinutes { get; set; }
        public string AvgDurationText { get; set; } = "00:00";
        public double PerformanceScore { get; set; }
        public string PerformanceSummary { get; set; } = "0.0 (C)";
        public DateTime LastIssueDate { get; set; }
        public string LastIssueDateText { get; set; } = "-";
    }
}
