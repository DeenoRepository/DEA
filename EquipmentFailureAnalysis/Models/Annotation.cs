using System;

namespace EquipmentFailureAnalysis.Models
{
    public class Annotation
    {
        // hour in day (0..24)
        public double Hour { get; set; }
        // description of the issue
        public string Description { get; set; } = string.Empty;
        // responsible person
        public string Responsible { get; set; } = string.Empty;
        // duration string like HH:mm
        public string Duration { get; set; } = string.Empty;
        // issue type (Настройка or Ремонт)
        public IssueType Type { get; set; }
    }
}
