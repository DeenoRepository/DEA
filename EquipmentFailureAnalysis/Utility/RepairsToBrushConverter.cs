using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    public class RepairsToBrushConverter : IValueConverter
    {
        // returns brush based on number of repairs: 0 -> green, 1 -> yellow, >=3 -> red, else neutral
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int cnt = 0;
            if (value is int i) cnt = i;
            else
            {
                int.TryParse(value?.ToString() ?? "0", out cnt);
            }

            // use project's palette colors
            if (cnt == 0)
                return new SolidColorBrush(Color.Parse("#E8F5E9")); // light green background
            if (cnt == 1)
                return new SolidColorBrush(Color.Parse("#FFFDE7")); // light yellow
            if (cnt >= 2)
                return new SolidColorBrush(Color.Parse("#FFEBEE")); // light red

            return new SolidColorBrush(Color.Parse("#FFFFFF"));
        }

        public object ConvertStatus(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int cnt = 0;
            if (value is int i) cnt = i;
            else
            {
                int.TryParse(value?.ToString() ?? "0", out cnt);
            }

            if (cnt == 0)
                return "No Repairs";
            if (cnt == 1)
                return "One Repair";
            if (cnt >= 3)
                return "Multiple Repairs";

            return "Unknown Status";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
