using Avalonia.Data.Converters;
using EquipmentFailureAnalysis.Models;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    public class PprCellToTooltipConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
                return null;

            var item = values[0] as PprScheduleItem;
            if (item == null)
                return null;

            int selectedYear = DateTime.Today.Year;
            if (values[1] is int yearVal)
            {
                selectedYear = yearVal;
            }

            int m = -1;
            if (parameter is string paramStr && int.TryParse(paramStr, out int monthIdx))
            {
                m = monthIdx;
            }
            else if (parameter is int monthInt)
            {
                m = monthInt;
            }

            if (m < 0 || m >= 12)
                return null;

            var plan = item.MonthlyPlans[m];
            if (string.IsNullOrEmpty(plan))
                return null;

            if (item.MonthlyCompletions[m])
            {
                return $"Выполнено: {plan}\n(Нажмите для отмены)";
            }

            int currentYear = DateTime.Today.Year;
            int currentMonthIndex = DateTime.Today.Month - 1; // 0-based

            if (selectedYear < currentYear)
            {
                return $"Просрочено: {plan}\n(Нажмите для отметки о выполнении)";
            }
            else if (selectedYear > currentYear)
            {
                return $"Запланировано: {plan}\n(Нажмите для отметки о выполнении)";
            }
            else
            {
                // Current year
                if (m < currentMonthIndex)
                {
                    return $"Просрочено: {plan}\n(Нажмите для отметки о выполнении)";
                }
                else if (m == currentMonthIndex)
                {
                    return $"В текущем месяце: {plan}\n(Нажмите для отметки о выполнении)";
                }
                else
                {
                    return $"Запланировано: {plan}\n(Нажмите для отметки о выполнении)";
                }
            }
        }
    }
}
