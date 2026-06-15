namespace EquipmentFailureAnalysis.Models
{
    public class DashboardTrendPoint
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public int IssuesCount { get; set; }
        public int RepairsCount { get; set; }
        public int SetupsCount { get; set; }
        public double AvgDurationMinutes { get; set; }
        public string AvgDurationText { get; set; } = "00:00";
        public double IntensityPercent { get; set; }
        public double SlaCompliancePercent { get; set; }

        // Formatting helpers for cleaner UI
        public bool HasData => IssuesCount > 0;
        public double RowOpacity => HasData ? 1.0 : 0.45;
        public string IssuesCountText => HasData ? IssuesCount.ToString() : "—";
        public string RepairsCountText => RepairsCount > 0 ? RepairsCount.ToString() : "—";
        public string SetupsCountText => SetupsCount > 0 ? SetupsCount.ToString() : "—";
        public string AvgDurationTextFormatted => (HasData && AvgDurationMinutes > 0) ? AvgDurationText : "—";
    }
}
