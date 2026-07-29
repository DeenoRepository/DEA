using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    public class StatusToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? Brushes.Green : Brushes.Transparent;
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
