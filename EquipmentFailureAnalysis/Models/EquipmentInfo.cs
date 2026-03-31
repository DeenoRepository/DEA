using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace EquipmentFailureAnalysis.Models
{
    public class EquipmentInfo
    {
        public int Uid { get; set; }
        public required string Title { get; set; }
        public Issue ?Issue { get; set; }
    }
}
