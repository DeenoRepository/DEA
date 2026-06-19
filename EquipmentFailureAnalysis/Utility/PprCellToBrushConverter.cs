using Avalonia.Data.Converters;
using Avalonia.Media;
using EquipmentFailureAnalysis.Models;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    public class PprCellToBrushConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
                return Brushes.Transparent;

            var item = values[0] as PprScheduleItem;
            if (item == null)
                return Brushes.Transparent;

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
                return Brushes.Transparent;

            var plan = item.MonthlyPlans[m];
            if (string.IsNullOrEmpty(plan))
                return Brushes.Transparent; // No plan -> transparent background

            if (item.MonthlyCompletions[m])
            {
                // Completed -> Green
                return Brush.Parse("#10B981");
            }

            int currentYear = DateTime.Today.Year;
            int currentMonthIndex = DateTime.Today.Month - 1; // 0-based

            if (selectedYear < currentYear)
            {
                // Past year and not completed -> Overdue (Red)
                return Brush.Parse("#EF4444");
            }
            else if (selectedYear > currentYear)
            {
                // Future year and not completed -> Planned (Gray)
                return Brush.Parse("#64748B");
            }
            else
            {
                // Current year
                if (m < currentMonthIndex)
                {
                    // Past month -> Overdue (Red)
                    return Brush.Parse("#EF4444");
                }
                else if (m == currentMonthIndex)
                {
                    // Current month -> Pending (Amber)
                    return Brush.Parse("#F59E0B");
                }
                else
                {
                    // Future month -> Planned (Gray)
                    return Brush.Parse("#64748B");
                }
            }
        }
    }
}
