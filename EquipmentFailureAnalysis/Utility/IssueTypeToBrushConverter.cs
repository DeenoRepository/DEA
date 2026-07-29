using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using EquipmentFailureAnalysis.Models;

namespace EquipmentFailureAnalysis.Utility
{
    public class IssueTypeToBrushConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is IssueType type)
            {
                if (type == IssueType.Ремонт)
                    return new SolidColorBrush(Color.Parse("#EF4444")); // Red
                if (type == IssueType.Настройка)
                    return new SolidColorBrush(Color.Parse("#F59E0B")); // Warning Yellow/Orange
            }
            return new SolidColorBrush(Color.Parse("#2563EB")); // Blue
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
