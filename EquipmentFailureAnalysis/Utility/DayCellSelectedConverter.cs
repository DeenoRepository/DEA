using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    public class DayCellSelectedBrushConverter : IMultiValueConverter
    {
        private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#4F46E5")); // Indigo 600
        private static readonly IBrush TodayBrush = new SolidColorBrush(Color.Parse("#2563EB")); // Blue
        private static readonly IBrush DefaultBrush = new SolidColorBrush(Color.Parse("#E2E8F0")); // Slate 200

        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2)
                return DefaultBrush;

            var cellDate = values[0] switch
            {
                DateTime dt => dt.Date,
                DateTimeOffset dto => dto.Date,
                _ => DateTime.MinValue
            };

            var selectedDate = values[1] switch
            {
                DateTime dt => dt.Date,
                DateTimeOffset dto => dto.Date,
                _ => DateTime.MinValue
            };

            if (cellDate == DateTime.MinValue)
                return DefaultBrush;

            if (cellDate == selectedDate)
                return SelectedBrush;

            if (cellDate == DateTime.Today)
                return TodayBrush;

            return DefaultBrush;
        }
    }

    public class DayCellSelectedThicknessConverter : IMultiValueConverter
    {
        private static readonly Avalonia.Thickness SelectedThickness = new Avalonia.Thickness(2.5);
        private static readonly Avalonia.Thickness TodayThickness = new Avalonia.Thickness(1.5);
        private static readonly Avalonia.Thickness DefaultThickness = new Avalonia.Thickness(1);

        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2)
                return DefaultThickness;

            var cellDate = values[0] switch
            {
                DateTime dt => dt.Date,
                DateTimeOffset dto => dto.Date,
                _ => DateTime.MinValue
            };

            var selectedDate = values[1] switch
            {
                DateTime dt => dt.Date,
                DateTimeOffset dto => dto.Date,
                _ => DateTime.MinValue
            };

            if (cellDate == DateTime.MinValue)
                return DefaultThickness;

            if (cellDate == selectedDate)
                return SelectedThickness;

            if (cellDate == DateTime.Today)
                return TodayThickness;

            return DefaultThickness;
        }
    }
}
