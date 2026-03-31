using System;
using System.Collections.Generic;
using System.Text;

namespace EquipmentFailureAnalysis.Models
{
    public enum FaultType
    {
        Mechanical,
        Electrical,
        Software,
        HumanError,
        Environmental
    }

    public class Fault
    {
        public FaultType Type { get; set; }
    }
}
