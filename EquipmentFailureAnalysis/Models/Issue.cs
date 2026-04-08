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

        // Тип, выявленный по тексту описания (если удалось определить)
        public IssueType? DetectedType { get; set; }

        // Флаг, указывающий на несоответствие между указанным Type и DetectedType
        public bool TypeSuspicious { get; set; } = false;

        // NLP-оценка вероятности того, что запись относится к ремонту (0..1)
        public double RepairProbability { get; set; }

        // NLP-оценка вероятности того, что запись относится к настройке (0..1)
        public double SetupProbability { get; set; }
    }

    public enum IssueType
    {
        Настройка,
        Ремонт
    }
}
