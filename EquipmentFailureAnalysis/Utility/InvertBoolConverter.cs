using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    /// <summary>Returns true when bound value is false (i.e. "invert" a boolean).</summary>
    public class InvertBoolConverter : IValueConverter
    {
        public static readonly InvertBoolConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return value;
        }
    }
}
