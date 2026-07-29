using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace EquipmentFailureAnalysis.Utility
{
    public class IndexToForegroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            int v = 0;
            if (value is int iv) v = iv;
            else if (value is string s && int.TryParse(s, out var sv)) v = sv;

            // compute gradient color same as ValueToColorConverter
            double t = Math.Max(0.0, Math.Min(1.0, v / 10.0));
            byte r, g, b;
            if (t <= 0.5)
            {
                double t2 = t / 0.5;
                byte rStart = 0, gStart = 200, bStart = 0;
                byte rMid = 255, gMid = 204, bMid = 0;
                r = (byte)(rStart + (rMid - rStart) * t2);
                g = (byte)(gStart + (gMid - gStart) * t2);
                b = (byte)(bStart + (bMid - bStart) * t2);
            }
            else
            {
                double t2 = (t - 0.5) / 0.5;
                byte rMid = 255, gMid = 204, bMid = 0;
                byte rEnd = 200, gEnd = 0, bEnd = 0;
                r = (byte)(rMid + (rEnd - rMid) * t2);
                g = (byte)(gMid + (gEnd - gMid) * t2);
                b = (byte)(bMid + (bEnd - bMid) * t2);
            }

            // luminance
            double lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            if (lum < 140) // dark background -> white text
                return Brushes.White;
            return Brushes.Black;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
