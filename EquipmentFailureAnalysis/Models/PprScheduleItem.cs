namespace EquipmentFailureAnalysis.Models
{
    public class PprScheduleItem
    {
        public string Subdivision { get; set; } = string.Empty;
        public string EquipmentTitle { get; set; } = string.Empty;
        public string InventoryNumber { get; set; } = string.Empty;
        public string CommissionYear { get; set; } = string.Empty;
        
        // Plans for Jan-Dec: values can be "ТО", "ТР" or null
        public string?[] MonthlyPlans { get; set; } = new string?[12];
        
        // Completion status for Jan-Dec: true if completed, false if not
        public bool[] MonthlyCompletions { get; set; } = new bool[12];
    }
}
