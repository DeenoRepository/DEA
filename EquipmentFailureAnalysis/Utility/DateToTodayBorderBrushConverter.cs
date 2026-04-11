using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    public class DateToTodayBorderBrushConverter : IValueConverter
    {
        private static readonly IBrush TodayBrush = new SolidColorBrush(Color.Parse("#2563EB"));
        private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#E6E6E6"));

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var date = value switch
            {
                DateTime dt => dt.Date,
                DateTimeOffset dto => dto.Date,
                _ => DateTime.MinValue
            };

            return date == DateTime.Today ? TodayBrush : DefaultBrush;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
