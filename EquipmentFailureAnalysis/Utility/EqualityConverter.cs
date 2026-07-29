using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    public class EqualityToBoolConverter : IMultiValueConverter
    {
        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
                return false;

            var first = values[0];
            var second = values[1];

            if (first == null && second == null)
                return true;

            if (first == null || second == null)
                return false;

            return first.Equals(second);
        }
    }
}
