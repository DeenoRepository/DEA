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
        // duration string like HH:mm
        public string Duration { get; set; } = string.Empty;
        // issue type (Настройка or Ремонт)
        public IssueType Type { get; set; }

        // пометка, что событие относится к задаче в процессе
        public bool IsInProgress { get; set; }
    }
}
