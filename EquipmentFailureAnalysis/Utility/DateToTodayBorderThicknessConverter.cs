using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    public class DateToTodayBorderThicknessConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var date = value switch
            {
                DateTime dt => dt.Date,
                DateTimeOffset dto => dto.Date,
                _ => DateTime.MinValue
            };

            return date == DateTime.Today ? new Thickness(2) : new Thickness(1);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
