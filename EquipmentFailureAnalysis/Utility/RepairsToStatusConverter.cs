using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    public class RepairsToStatusConverter : IValueConverter
    {
        // Returns user-friendly text based on repairs count
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            int cnt = 0;
            if (value is int i) cnt = i;
            else
                int.TryParse(value?.ToString() ?? "0", out cnt);

            // Return only the short status message (no count/dash)
            if (cnt == 0) return "Отлично - нет ремонтов";
            if (cnt == 1) return "Внимание!";
            if (cnt >= 2) return "Критически!";
            return "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
