using System;

namespace EquipmentFailureAnalysis.Models
{
    public class TimelinePoint
    {
        // Hour in day as fractional hours (0.0..24.0)
        public double Hour { get; set; }
        // Value: 0 = working, 1 = maintenance
        public int Value { get; set; }
    }
}
