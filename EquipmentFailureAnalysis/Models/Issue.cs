using System;
using System.Collections.Generic;
using System.Text;

namespace EquipmentFailureAnalysis.Models
{
    public class Issue
    {
        public DateTime Created { get; set; }
        public DateTime Resolved { get; set; }
        public required string Description { get; set; }
    }
}
