using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using EquipmentFailureAnalysis.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EquipmentFailureAnalysis.Views
{
    public class MonthlyTrendsChartControl : Control
    {
        private static readonly Typeface UiTypeface = new Typeface("Segoe UI");

        private Point? _hoverPoint;
        private int _hoveredIndex = -1;

        public static readonly StyledProperty<IList?> ItemsProperty =
            AvaloniaProperty.Register<MonthlyTrendsChartControl, IList?>(nameof(Items));

        public IList? Items
        {
            get => GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public static readonly StyledProperty<bool> IsSlaChartProperty =
            AvaloniaProperty.Register<MonthlyTrendsChartControl, bool>(nameof(IsSlaChart));

        public bool IsSlaChart
        {
            get => GetValue(IsSlaChartProperty);
            set => SetValue(IsSlaChartProperty, value);
        }

        public static readonly StyledProperty<bool> IsMttrChartProperty =
            AvaloniaProperty.Register<MonthlyTrendsChartControl, bool>(nameof(IsMttrChart));

        public bool IsMttrChart
        {
            get => GetValue(IsMttrChartProperty);
            set => SetValue(IsMttrChartProperty, value);
        }

        public MonthlyTrendsChartControl()
        {
            this.GetObservable(ItemsProperty).Subscribe(_ => InvalidateVisual());
            this.GetObservable(IsSlaChartProperty).Subscribe(_ => InvalidateVisual());
            this.GetObservable(IsMttrChartProperty).Subscribe(_ => InvalidateVisual());
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            _hoverPoint = e.GetPosition(this);
            InvalidateVisual();
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            _hoverPoint = null;
            _hoveredIndex = -1;
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var bounds = Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            // Draw a transparent background to capture mouse inputs across the whole area
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, bounds.Width, bounds.Height));

            var source = Items?.OfType<DashboardTrendPoint>().ToList() ?? new List<DashboardTrendPoint>();

            var left = 30.0;
            var right = 8.0;
            var top = 10.0;
            var bottom = 34.0;

            var plotWidth = Math.Max(1, bounds.Width - left - right);
            var plotHeight = Math.Max(1, bounds.Height - top - bottom);
            var plotBottom = top + plotHeight;

            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(80, 209, 219, 232)), 1);
            for (int i = 0; i <= 4; i++)
            {
                var y = top + i * (plotHeight / 4.0);
                context.DrawLine(gridPen, new Point(left, y), new Point(left + plotWidth, y));
            }

            if (source.Count == 0)
            {
                var noData = new FormattedText("Нет данных", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, UiTypeface, 12, new SolidColorBrush(Color.Parse("#6B7280")));
                context.DrawText(noData, new Point(left + 8, top + plotHeight / 2.0 - 8));
                return;
            }

            var values = source
                .Select(x => IsSlaChart ? x.SlaCompliancePercent : (IsMttrChart ? x.AvgDurationMinutes : x.IssuesCount))
                .Select(v => Math.Max(0, v))
                .ToArray();

            var maxValue = IsSlaChart ? 100.0 : Math.Max(1.0, values.Max());
            var axisBrush = new SolidColorBrush(Color.Parse("#6B7280"));

            var topAxisValueText = IsMttrChart ? FormatDuration(maxValue) : $"{maxValue:0.#}";
            var topAxisText = new FormattedText(topAxisValueText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, UiTypeface, 10, axisBrush);
            var bottomAxisText = new FormattedText("0", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, UiTypeface, 10, axisBrush);
            context.DrawText(topAxisText, new Point(2, top - 5));
            context.DrawText(bottomAxisText, new Point(8, plotBottom - 8));

            var points = new List<Point>(source.Count);
            var step = source.Count > 1 ? plotWidth / (source.Count - 1.0) : 0;

            for (int i = 0; i < source.Count; i++)
            {
                var x = source.Count > 1 ? left + i * step : left + plotWidth / 2.0;
                var value = values[i];
                var y = plotBottom - (value / maxValue) * plotHeight;
                points.Add(new Point(x, y));
            }

            var lineColor = IsSlaChart
                ? Color.Parse("#4F46E5") // Enterprise Indigo theme
                : (IsMttrChart ? Color.Parse("#EF4444") : Color.Parse("#10B981"));

            var areaGeometry = new StreamGeometry();
            using (var g = areaGeometry.Open())
            {
                g.BeginFigure(new Point(points[0].X, plotBottom), true);
                g.LineTo(points[0]);
                
                double tension = 0.25;
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Point p0 = points[Math.Max(i - 1, 0)];
                    Point p1 = points[i];
                    Point p2 = points[i + 1];
                    Point p3 = points[Math.Min(i + 2, points.Count - 1)];

                    Point cp1 = p1 + new Point(p2.X - p0.X, p2.Y - p0.Y) * tension;
                    Point cp2 = p2 - new Point(p3.X - p1.X, p3.Y - p1.Y) * tension;

                    cp1 = new Point(Math.Clamp(cp1.X, p1.X, p2.X), cp1.Y);
                    cp2 = new Point(Math.Clamp(cp2.X, p1.X, p2.X), cp2.Y);

                    g.CubicBezierTo(cp1, cp2, p2);
                }

                g.LineTo(new Point(points[^1].X, plotBottom));
                g.EndFigure(true);
            }

            var gradientBrush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(90, lineColor.R, lineColor.G, lineColor.B), 0.0),
                    new GradientStop(Color.FromArgb(5, lineColor.R, lineColor.G, lineColor.B), 1.0)
                }
            };

            context.DrawGeometry(gradientBrush, null, areaGeometry);

            var lineGeometry = new StreamGeometry();
            using (var g = lineGeometry.Open())
            {
                g.BeginFigure(points[0], false);
                double tension = 0.25;
                for (int i = 0; i < points.Count - 1; i++)
                {
                    Point p0 = points[Math.Max(i - 1, 0)];
                    Point p1 = points[i];
                    Point p2 = points[i + 1];
                    Point p3 = points[Math.Min(i + 2, points.Count - 1)];

                    Point cp1 = p1 + new Point(p2.X - p0.X, p2.Y - p0.Y) * tension;
                    Point cp2 = p2 - new Point(p3.X - p1.X, p3.Y - p1.Y) * tension;

                    cp1 = new Point(Math.Clamp(cp1.X, p1.X, p2.X), cp1.Y);
                    cp2 = new Point(Math.Clamp(cp2.X, p1.X, p2.X), cp2.Y);

                    g.CubicBezierTo(cp1, cp2, p2);
                }
                g.EndFigure(false);
            }

            context.DrawGeometry(null, new Pen(new SolidColorBrush(lineColor), 2.5), lineGeometry);

            // Draw standard X axis labels
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                var label = source[i].PeriodLabel ?? string.Empty;
                if (label.Length > 8)
                    label = label.Substring(0, 8) + "…";
                var labelLayout = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, UiTypeface, 10, axisBrush);
                context.DrawText(labelLayout, new Point(p.X - Math.Min(20, label.Length * 3), plotBottom + 8));
            }

            // Draw interactive hover indicators
            _hoveredIndex = -1;
            if (_hoverPoint.HasValue)
            {
                var hx = _hoverPoint.Value.X;
                double minDistance = double.MaxValue;
                for (int i = 0; i < points.Count; i++)
                {
                    var dist = Math.Abs(points[i].X - hx);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        _hoveredIndex = i;
                    }
                }

                if (_hoveredIndex >= 0 && _hoveredIndex < points.Count)
                {
                    var p = points[_hoveredIndex];

                    // Draw vertical guide line
                    var guidePen = new Pen(new SolidColorBrush(Color.FromArgb(160, lineColor.R, lineColor.G, lineColor.B)), 1.5, dashStyle: DashStyle.Dash);
                    context.DrawLine(guidePen, new Point(p.X, top), new Point(p.X, plotBottom));

                    // Glow circle & inner dot
                    var glowBrush = new SolidColorBrush(Color.FromArgb(45, lineColor.R, lineColor.G, lineColor.B));
                    context.DrawGeometry(glowBrush, null, new EllipseGeometry(new Rect(p.X - 9, p.Y - 9, 18, 18)));
                    context.DrawGeometry(Brushes.White, new Pen(new SolidColorBrush(lineColor), 3), new EllipseGeometry(new Rect(p.X - 4.5, p.Y - 4.5, 9, 9)));

                    // Tooltip
                    var valueText = IsSlaChart
                        ? $"{values[_hoveredIndex]:0.#}%"
                        : (IsMttrChart ? FormatDuration(values[_hoveredIndex]) : $"{values[_hoveredIndex]:0}");
                    var periodText = source[_hoveredIndex].PeriodLabel ?? string.Empty;
                    var tooltipText = $"{periodText}\n{valueText}";

                    var tooltipLayout = new FormattedText(tooltipText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, UiTypeface, 11, Brushes.White);
                    var tooltipWidth = tooltipLayout.Width + 16;
                    var tooltipHeight = tooltipLayout.Height + 12;

                    var tx = Math.Clamp(p.X + 12, left, bounds.Width - tooltipWidth - 8);
                    var ty = Math.Clamp(p.Y - tooltipHeight - 12, top, bounds.Height - tooltipHeight - 8);

                    var tooltipRect = new Rect(tx, ty, tooltipWidth, tooltipHeight);
                    var tooltipBg = new SolidColorBrush(Color.Parse("#1E293B")); // Slate 800
                    var tooltipBorder = new Pen(new SolidColorBrush(Color.Parse("#475569")), 1); // Slate 600
                    context.DrawRectangle(tooltipBg, tooltipBorder, tooltipRect, 6, 6);
                    context.DrawText(tooltipLayout, new Point(tx + 8, ty + 6));
                }
            }

            // Draw regular point dots (non-hovered or all if not hover)
            for (int i = 0; i < points.Count; i++)
            {
                if (i == _hoveredIndex)
                    continue;

                var p = points[i];
                context.DrawGeometry(Brushes.White, new Pen(new SolidColorBrush(lineColor), 2), new EllipseGeometry(new Rect(p.X - 3.5, p.Y - 3.5, 7, 7)));

                var valueText = IsSlaChart
                    ? $"{values[i]:0.#}%"
                    : (IsMttrChart ? FormatDuration(values[i]) : $"{values[i]:0}");
                var valueLayout = new FormattedText(valueText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, UiTypeface, 9, new SolidColorBrush(lineColor));
                context.DrawText(valueLayout, new Point(p.X - valueText.Length * 2.5, p.Y - 14));
            }
        }

        private static string FormatDuration(double totalMinutes)
        {
            var safeMinutes = Math.Max(0, (int)Math.Round(totalMinutes));
            var hours = safeMinutes / 60;
            var minutes = safeMinutes % 60;
            return $"{hours:00}:{minutes:00}";
        }
    }
}
