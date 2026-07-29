using System;
using System.Collections.Generic;

namespace EquipmentFailureAnalysis.Models
{
    public enum PeriodGranularity
    {
        Monthly,
        Quarterly
    }

    public sealed class PeriodLossBucket
    {
        public string PeriodKey { get; init; } = string.Empty;
        public string PeriodLabel { get; init; } = string.Empty;
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public double RepairMinutes { get; set; }
        public double SetupMinutes { get; set; }
        public double TotalMinutes => RepairMinutes + SetupMinutes;
        public int RepairCount { get; set; }
        public int SetupCount { get; set; }
        public int TotalCount => RepairCount + SetupCount;
    }

    public sealed class DowntimeIssueDetail
    {
        public string EquipmentIdKey { get; init; } = string.Empty;
        public string EquipmentTitle { get; init; } = string.Empty;
        public string InventoryNumber { get; init; } = string.Empty;
        public string Subdivision { get; init; } = string.Empty;
        public string JiraIssueKey { get; init; } = string.Empty;
        public string IssueType { get; init; } = string.Empty;
        public DateTime Start { get; init; }
        public DateTime End { get; init; }
        public double DurationMinutes { get; init; }
        public string Responsible { get; init; } = string.Empty;
        public string Reporter { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Comments { get; init; } = string.Empty;
    }

    public sealed class EquipmentDowntimeLossRow
    {
        public string EquipmentIdKey { get; init; } = string.Empty;
        public string EquipmentTitle { get; init; } = string.Empty;
        public string InventoryNumber { get; init; } = string.Empty;
        public string Subdivision { get; init; } = string.Empty;
        public double TotalRepairMinutes { get; set; }
        public double TotalSetupMinutes { get; set; }
        public double TotalDowntimeMinutes => TotalRepairMinutes + TotalSetupMinutes;
        public int TotalRepairCount { get; set; }
        public int TotalSetupCount { get; set; }
        public int TotalIssuesCount => TotalRepairCount + TotalSetupCount;
        public Dictionary<string, PeriodLossBucket> PeriodBuckets { get; init; } = new Dictionary<string, PeriodLossBucket>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class EquipmentDowntimeLossReport
    {
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public PeriodGranularity Granularity { get; init; }
        public List<PeriodLossBucket> PeriodHeaders { get; init; } = new List<PeriodLossBucket>();
        public List<EquipmentDowntimeLossRow> Rows { get; init; } = new List<EquipmentDowntimeLossRow>();
        public List<DowntimeIssueDetail> AllIssueDetails { get; init; } = new List<DowntimeIssueDetail>();
        public double TotalRepairMinutes { get; set; }
        public double TotalSetupMinutes { get; set; }
        public double GrandTotalMinutes => TotalRepairMinutes + TotalSetupMinutes;
        public int TotalRepairCount { get; set; }
        public int TotalSetupCount { get; set; }
        public int GrandTotalCount => TotalRepairCount + TotalSetupCount;
    }
}
