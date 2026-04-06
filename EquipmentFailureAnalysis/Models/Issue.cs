using System;
using System.Collections.Generic;
using System.Text;

namespace EquipmentFailureAnalysis.Models
{
    public class Issue
    {
        // Дата начала неисправности / начала работ
        public DateTime Start { get; set; }

        // Дата окончания неисправности / окончания работ
        public DateTime End { get; set; }

        // Описание неисправности
        public required string Description { get; set; }

        // Тип неисправности: настройка или ремонт
        public IssueType Type { get; set; }

        // Ответственный за выполнение ремонта или настройки
        public string? Responsible { get; set; }
    }

    public enum IssueType
    {
        Настройка,
        Ремонт
    }
}
