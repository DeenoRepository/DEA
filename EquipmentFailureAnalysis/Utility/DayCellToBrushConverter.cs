using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    public class DayCellToBrushConverter : IMultiValueConverter
    {
        private readonly ValueToColorConverter _valueConv = new ValueToColorConverter();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values == null || values.Count < 2)
                return Brushes.Transparent;

            var cell = values[0] as Models.DayCell;
            var mode = values[1] as string;

            if (cell != null)
            {
                if (!cell.IsValid)
                    return Brushes.Transparent;

                var dow = cell.Date.DayOfWeek;
                bool isWeekend = (dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday);

                // If weekend and no issues, use subtle weekend background
                if (isWeekend && cell.Index == 0)
                {
                    var c = new Color(255, 227, 242, 253); // light blue #E3F2FD
                    return new SolidColorBrush(c);
                }

                // otherwise, fall back to value-to-color mapping for issue intensity
                try
                {
                    return _valueConv.Convert(cell.Index, targetType, mode, culture);
                }
                catch
                {
                    return Brushes.Transparent;
                }
            }

            return Brushes.Transparent;
        }
    }
}
