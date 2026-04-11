using Avalonia;
using Avalonia.Controls;
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

        public MonthlyTrendsChartControl()
        {
            this.GetObservable(ItemsProperty).Subscribe(_ => InvalidateVisual());
            this.GetObservable(IsSlaChartProperty).Subscribe(_ => InvalidateVisual());
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var bounds = Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

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
                .Select(x => IsSlaChart ? x.SlaCompliancePercent : x.IssuesCount)
                .Select(v => Math.Max(0, v))
                .ToArray();

            var maxValue = IsSlaChart ? 100.0 : Math.Max(1.0, values.Max());
            var axisBrush = new SolidColorBrush(Color.Parse("#6B7280"));

            var topAxisText = new FormattedText($"{maxValue:0.#}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, UiTypeface, 10, axisBrush);
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

            var lineColor = IsSlaChart ? Color.Parse("#0F766E") : Color.Parse("#1D4ED8");
            var fillColor = IsSlaChart ? Color.FromArgb(56, 15, 118, 110) : Color.FromArgb(56, 29, 78, 216);

            var areaGeometry = new StreamGeometry();
            using (var g = areaGeometry.Open())
            {
                g.BeginFigure(new Point(points[0].X, plotBottom), true);
                foreach (var p in points)
                    g.LineTo(p);
                g.LineTo(new Point(points[^1].X, plotBottom));
                g.EndFigure(true);
            }

            context.DrawGeometry(new SolidColorBrush(fillColor), null, areaGeometry);

            var lineGeometry = new StreamGeometry();
            using (var g = lineGeometry.Open())
            {
                g.BeginFigure(points[0], false);
                foreach (var p in points.Skip(1))
                    g.LineTo(p);
                g.EndFigure(false);
            }

            context.DrawGeometry(null, new Pen(new SolidColorBrush(lineColor), 2), lineGeometry);

            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                context.DrawGeometry(Brushes.White, new Pen(new SolidColorBrush(lineColor), 2), new EllipseGeometry(new Rect(p.X - 3.5, p.Y - 3.5, 7, 7)));

                var valueText = IsSlaChart ? $"{values[i]:0.#}%" : $"{values[i]:0}";
                var valueLayout = new FormattedText(valueText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, UiTypeface, 10, new SolidColorBrush(lineColor));
                context.DrawText(valueLayout, new Point(p.X - valueText.Length * 3, p.Y - 16));

                var label = source[i].PeriodLabel ?? string.Empty;
                if (label.Length > 8)
                    label = label.Substring(0, 8) + "…";
                var labelLayout = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, UiTypeface, 10, axisBrush);
                context.DrawText(labelLayout, new Point(p.X - Math.Min(20, label.Length * 3), plotBottom + 8));
            }
        }
    }
}
