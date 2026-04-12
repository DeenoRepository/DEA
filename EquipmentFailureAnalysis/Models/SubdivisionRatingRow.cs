namespace EquipmentFailureAnalysis.Models
{
    public class SubdivisionRatingRow
    {
        public string Subdivision { get; set; } = "-";
        public int IssuesCount { get; set; }
        public int ActiveEmployees { get; set; }
        public double SlaCompliancePercent { get; set; }
        public double MttrMinutes { get; set; }
        public string MttrText { get; set; } = "00:00";
        public double PerformanceScore { get; set; }
    }
}
