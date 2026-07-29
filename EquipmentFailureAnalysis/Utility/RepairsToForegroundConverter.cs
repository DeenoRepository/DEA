using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    public class RepairsToForegroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            int cnt = 0;
            if (value is int i) cnt = i;
            else
                int.TryParse(value?.ToString() ?? "0", out cnt);

            // Choose readable foreground color depending on repairs count / background
            if (cnt == 0)
                return new SolidColorBrush(Color.Parse("#2E7D32")); // dark green
            if (cnt == 1)
                return new SolidColorBrush(Color.Parse("#8A6B00")); // dark yellow/brown
            if (cnt >= 2)
                return new SolidColorBrush(Color.Parse("#B00020")); // dark red

            return new SolidColorBrush(Color.Parse("#222222"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
