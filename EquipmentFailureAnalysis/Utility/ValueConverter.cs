using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace EquipmentFailureAnalysis.Utility
{
    public class ValueToColorConverter : IValueConverter
    {
        public const string FailureHeatmapKey = "FailureAnalysis";
        public const string DowntimeHeatmapKey = "DowntimeAnalysis";

        private static readonly Dictionary<string, (int Min, int Max)> HeatmapRanges = new Dictionary<string, (int Min, int Max)>(StringComparer.OrdinalIgnoreCase)
        {
            [FailureHeatmapKey] = (0, 10),
            [DowntimeHeatmapKey] = (0, 10)
        };

        public static int HeatmapMinValue
        {
            get => GetHeatmapRange(FailureHeatmapKey).Min;
            set => SetHeatmapRange(FailureHeatmapKey, value, HeatmapMaxValue);
        }

        public static int HeatmapMaxValue
        {
            get => GetHeatmapRange(FailureHeatmapKey).Max;
            set => SetHeatmapRange(FailureHeatmapKey, HeatmapMinValue, value);
        }

        public static (int Min, int Max) GetHeatmapRange(string? key)
        {
            var effectiveKey = string.IsNullOrWhiteSpace(key) ? FailureHeatmapKey : key.Trim();
            if (HeatmapRanges.TryGetValue(effectiveKey, out var range))
                return range;

            return HeatmapRanges[FailureHeatmapKey];
        }

        public static void SetHeatmapRange(string key, int min, int max)
        {
            var effectiveKey = string.IsNullOrWhiteSpace(key) ? FailureHeatmapKey : key.Trim();
            var normalizedMin = Math.Max(0, min);
            var normalizedMax = Math.Max(normalizedMin + 1, max);
            HeatmapRanges[effectiveKey] = (normalizedMin, normalizedMax);
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int v = 0;
            if (value is int iv)
                v = iv;
            else if (value is string s && int.TryParse(s, out var sv))
                v = sv;

            var (min, max) = GetHeatmapRange(parameter as string);

            var range = max - min;
            double t = Math.Max(0.0, Math.Min(1.0, (v - min) / (double)range));

            // Use a slightly brighter gradient palette: green -> yellow -> orange -> red
            // green = (129, 199, 132) #81C784
            // yellow = (255, 238, 88)  #FFEE58
            // orange = (255, 183, 77)  #FFB74D
            // red = (239, 83, 80)      #EF5350
            byte r, g, b;
            if (t <= 0.5)
            {
                // interpolate green -> yellow
                double t2 = t / 0.5; // 0..1
                byte rStart = 129, gStart = 199, bStart = 132; // green
                byte rEnd = 255, gEnd = 238, bEnd = 88; // yellow
                r = (byte)(rStart + (rEnd - rStart) * t2);
                g = (byte)(gStart + (gEnd - gStart) * t2);
                b = (byte)(bStart + (bEnd - bStart) * t2);
            }
            else
            {
                // interpolate yellow -> red through orange
                double t2 = (t - 0.5) / 0.5; // 0..1
                // first half of this segment: yellow -> orange, second half: orange -> red
                if (t2 <= 0.5)
                {
                    double t3 = t2 / 0.5; // 0..1
                    byte rStart = 255, gStart = 238, bStart = 88; // yellow
                    byte rMid = 255, gMid = 183, bMid = 77; // orange
                    r = (byte)(rStart + (rMid - rStart) * t3);
                    g = (byte)(gStart + (gMid - gStart) * t3);
                    b = (byte)(bStart + (bMid - bStart) * t3);
                }
                else
                {
                    double t3 = (t2 - 0.5) / 0.5; // 0..1
                    byte rMid = 255, gMid = 183, bMid = 77; // orange
                    byte rEnd = 239, gEnd = 83, bEnd = 80; // red
                    r = (byte)(rMid + (rEnd - rMid) * t3);
                    g = (byte)(gMid + (gEnd - gMid) * t3);
                    b = (byte)(bMid + (bEnd - bMid) * t3);
                }
            }

            var c = new Color(255, r, g, b);
            return new SolidColorBrush(c);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
