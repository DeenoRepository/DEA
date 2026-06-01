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
    public class ParetoChartControl : Control
    {
        private static readonly Typeface UiTypeface = new Typeface("Segoe UI");

        private Point? _hoverPoint;
        private int _hoveredIndex = -1;

        public static readonly StyledProperty<IList?> ItemsProperty =
            AvaloniaProperty.Register<ParetoChartControl, IList?>(nameof(Items));

        public IList? Items
        {
            get => GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public ParetoChartControl()
        {
            this.GetObservable(ItemsProperty).Subscribe(_ => InvalidateVisual());
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

            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, bounds.Width, bounds.Height));

            var source = Items?.OfType<ParetoPoint>().ToList() ?? new List<ParetoPoint>();

            var left = 45.0;
            var right = 45.0;
            var top = 20.0;
            var bottom = 34.0;

            var plotWidth = Math.Max(1, bounds.Width - left - right);
            var plotHeight = Math.Max(1, bounds.Height - top - bottom);
            var plotBottom = top + plotHeight;

            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(50, 209, 219, 232)), 1);
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

            var maxVal = Math.Max(1.0, source.Max(x => x.Value));

            var axisBrush = new SolidColorBrush(Color.Parse("#6B7280"));
            
            var leftAxisTopText = new FormattedText($"{maxVal:0}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, UiTypeface, 9, axisBrush);
            var leftAxisBottomText = new FormattedText("0", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, UiTypeface, 9, axisBrush);
            context.DrawText(leftAxisTopText, new Point(10, top - 5));
            context.DrawText(leftAxisBottomText, new Point(20, plotBottom - 8));

            var rightAxisTopText = new FormattedText("100%", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, UiTypeface, 9, axisBrush);
            var rightAxisBottomText = new FormattedText("0%", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, UiTypeface, 9, axisBrush);
            context.DrawText(rightAxisTopText, new Point(bounds.Width - right + 8, top - 5));
            context.DrawText(rightAxisBottomText, new Point(bounds.Width - right + 8, plotBottom - 8));

            var count = source.Count;
            var colWidth = plotWidth / count;
            var barPadding = Math.Max(2.0, colWidth * 0.15);
            var actualBarWidth = colWidth - 2 * barPadding;

            var barColor = Color.Parse("#6366F1"); // Indigo 500
            var curveColor = Color.Parse("#EF4444"); // Red 500

            var bars = new List<Rect>();
            for (int i = 0; i < count; i++)
            {
                var val = source[i].Value;
                var h = (val / maxVal) * plotHeight;
                var bx = left + i * colWidth + barPadding;
                var by = plotBottom - h;
                var barRect = new Rect(bx, by, actualBarWidth, h);
                bars.Add(barRect);
            }

            _hoveredIndex = -1;
            if (_hoverPoint.HasValue)
            {
                var hx = _hoverPoint.Value.X;
                if (hx >= left && hx <= left + plotWidth)
                {
                    _hoveredIndex = (int)((hx - left) / colWidth);
                    _hoveredIndex = Math.Clamp(_hoveredIndex, 0, count - 1);
                }
            }

            for (int i = 0; i < count; i++)
            {
                var barRect = bars[i];
                var fill = i == _hoveredIndex 
                    ? new SolidColorBrush(Color.Parse("#4F46E5"))
                    : new SolidColorBrush(barColor);
                
                context.DrawRectangle(fill, null, barRect, 4, 4);

                var label = source[i].Label;
                if (label.Length > 10) label = label.Substring(0, 8) + "..";
                var labelLayout = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, UiTypeface, 9, axisBrush);
                context.DrawText(labelLayout, new Point(barRect.X + barRect.Width / 2.0 - labelLayout.Width / 2.0, plotBottom + 6));
            }

            var curvePoints = new List<Point>();
            for (int i = 0; i < count; i++)
            {
                var cx = left + i * colWidth + colWidth / 2.0;
                var cy = plotBottom - (source[i].CumulativePercent / 100.0) * plotHeight;
                curvePoints.Add(new Point(cx, cy));
            }

            var curveGeometry = new StreamGeometry();
            using (var g = curveGeometry.Open())
            {
                g.BeginFigure(curvePoints[0], false);
                for (int i = 1; i < count; i++)
                {
                    g.LineTo(curvePoints[i]);
                }
                g.EndFigure(false);
            }

            context.DrawGeometry(null, new Pen(new SolidColorBrush(curveColor), 2.5), curveGeometry);

            for (int i = 0; i < count; i++)
            {
                var p = curvePoints[i];
                IBrush circleBrush = i == _hoveredIndex ? Brushes.White : new SolidColorBrush(curveColor);
                var circleBorder = i == _hoveredIndex ? new Pen(new SolidColorBrush(curveColor), 2.5) : null;
                context.DrawGeometry(circleBrush, circleBorder, new EllipseGeometry(new Rect(p.X - 3.5, p.Y - 3.5, 7, 7)));
            }

            if (_hoveredIndex >= 0 && _hoveredIndex < count)
            {
                var p = curvePoints[_hoveredIndex];

                var guidePen = new Pen(new SolidColorBrush(Color.FromArgb(120, curveColor.R, curveColor.G, curveColor.B)), 1.5, dashStyle: DashStyle.Dash);
                context.DrawLine(guidePen, new Point(p.X, top), new Point(p.X, plotBottom));

                var pt = source[_hoveredIndex];
                var tooltipText = $"{pt.Label}\nОтказов: {pt.Value}\nКумулятивно: {pt.CumulativePercent:0.#}%";

                var tooltipLayout = new FormattedText(tooltipText, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, UiTypeface, 10, Brushes.White);
                var tooltipWidth = tooltipLayout.Width + 14;
                var tooltipHeight = tooltipLayout.Height + 10;

                var tx = Math.Clamp(p.X + 10, left, bounds.Width - tooltipWidth - 8);
                var ty = Math.Clamp(p.Y - tooltipHeight - 10, top, bounds.Height - tooltipHeight - 8);

                var tooltipRect = new Rect(tx, ty, tooltipWidth, tooltipHeight);
                var tooltipBg = new SolidColorBrush(Color.Parse("#0F172A"));
                var tooltipBorder = new Pen(new SolidColorBrush(Color.Parse("#334155")), 1);

                context.DrawRectangle(tooltipBg, tooltipBorder, tooltipRect, 6, 6);
                context.DrawText(tooltipLayout, new Point(tx + 7, ty + 5));
            }
        }
    }
}
