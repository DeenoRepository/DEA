namespace EquipmentFailureAnalysis.Models
{
    public class ParetoPoint
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public double CumulativePercent { get; set; }
    }
}
