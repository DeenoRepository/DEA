using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    /// <summary>Computes a mid-point value for the heatmap legend from a single bound value (the upper bound).</summary>
    public class HeatmapMidValueConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is IConvertible conv)
            {
                try
                {
                    var upper = System.Convert.ToDouble(conv, CultureInfo.InvariantCulture);
                    // Default min is 0, but we don't have it here. We split upper/2 as a reasonable default.
                    // The XAML computes the lower bound of the second bucket by (upper/2).
                    return System.Math.Max(1, (int)System.Math.Round(upper / 2.0));
                }
                catch { return 0; }
            }
            return 0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
