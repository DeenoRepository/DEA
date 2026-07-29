using System;
using System.Collections.ObjectModel;

namespace EquipmentFailureAnalysis.Models
{
    public class MonthRow
    {
        public string MonthName { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public ObservableCollection<DayCell> Days { get; set; } = new ObservableCollection<DayCell>();
    }

    public class DayCell
    {
        public int DayNumber { get; set; }
        public int Index { get; set; }
        public bool IsValid { get; set; }
        public DateTime Date { get; set; }
        public bool IsActive => Index > 0 && IsValid;
    }
}
