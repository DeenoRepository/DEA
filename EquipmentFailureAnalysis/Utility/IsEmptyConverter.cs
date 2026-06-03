using Avalonia.Data.Converters;
using System;
using System.Collections;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    /// <summary>
    /// Returns true (Visible) when the bound collection is null or has zero items.
    /// Used to drive empty-state placeholders.
    /// </summary>
    public class IsEmptyConverter : IValueConverter
    {
        public static readonly IsEmptyConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value == null) return true;
            if (value is string s) return string.IsNullOrEmpty(s);
            if (value is ICollection col) return col.Count == 0;
            if (value is IEnumerable enumerable)
            {
                foreach (var _ in enumerable) return false;
                return true;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
