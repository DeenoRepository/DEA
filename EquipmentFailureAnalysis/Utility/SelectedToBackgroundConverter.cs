using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    public class SelectedToBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? new SolidColorBrush(Color.Parse("#1976D2")) : Brushes.White;
            return Brushes.White;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
