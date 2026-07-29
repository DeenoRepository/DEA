using Avalonia.Data.Converters;
using EquipmentFailureAnalysis.Models;
using System;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    public class PprCellHasPlanConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is PprScheduleItem item && parameter is string paramStr && int.TryParse(paramStr, out int m))
            {
                if (m >= 0 && m < 12)
                {
                    return !string.IsNullOrEmpty(item.MonthlyPlans[m]);
                }
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
