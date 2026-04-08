namespace EquipmentFailureAnalysis.Models
{
    public class DashboardTrendPoint
    {
        public string PeriodLabel { get; set; } = string.Empty;
        public int IssuesCount { get; set; }
        public int RepairsCount { get; set; }
        public int SetupsCount { get; set; }
        public string AvgDurationText { get; set; } = "00:00";
        public double IntensityPercent { get; set; }
        public double SlaCompliancePercent { get; set; }
    }
}
