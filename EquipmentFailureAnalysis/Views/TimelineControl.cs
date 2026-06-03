using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace EquipmentFailureAnalysis.Views
{
    public class TimelineControl : Control
    {
        private static readonly Typeface UiTypeface = new Typeface("Segoe UI");
        private string? _currentTooltipText;

        public static readonly StyledProperty<IList?> ItemsProperty = AvaloniaProperty.Register<TimelineControl, IList?>(nameof(Items));

        public IList? Items
        {
            get => GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public static readonly StyledProperty<IList?> RepairsItemsProperty = AvaloniaProperty.Register<TimelineControl, IList?>(nameof(RepairsItems));

        public IList? RepairsItems
        {
            get => GetValue(RepairsItemsProperty);
            set => SetValue(RepairsItemsProperty, value);
        }

        public static readonly StyledProperty<IList?> SetupsItemsProperty = AvaloniaProperty.Register<TimelineControl, IList?>(nameof(SetupsItems));

        public IList? SetupsItems
        {
            get => GetValue(SetupsItemsProperty);
            set => SetValue(SetupsItemsProperty, value);
        }

        public static readonly StyledProperty<IList?> AnnotationsProperty = AvaloniaProperty.Register<TimelineControl, IList?>(nameof(Annotations));

        public IList? Annotations
        {
            get => GetValue(AnnotationsProperty);
            set => SetValue(AnnotationsProperty, value);
        }

        public static readonly StyledProperty<ICommand?> AnnotationSelectedCommandProperty =
            AvaloniaProperty.Register<TimelineControl, ICommand?>(nameof(AnnotationSelectedCommand));

        public ICommand? AnnotationSelectedCommand
        {
            get => GetValue(AnnotationSelectedCommandProperty);
            set => SetValue(AnnotationSelectedCommandProperty, value);
        }

        public TimelineControl()
        {
            this.GetObservable(ItemsProperty).Subscribe(items =>
            {
                Unsubscribe(itemsChanged);
                itemsChanged = items as INotifyCollectionChanged;
                Subscribe(itemsChanged);
                InvalidateVisual();
            });

            this.GetObservable(RepairsItemsProperty).Subscribe(items =>
            {
                Unsubscribe(repairsItemsChanged);
                repairsItemsChanged = items as INotifyCollectionChanged;
                Subscribe(repairsItemsChanged);
                InvalidateVisual();
            });

            this.GetObservable(SetupsItemsProperty).Subscribe(items =>
            {
                Unsubscribe(setupsItemsChanged);
                setupsItemsChanged = items as INotifyCollectionChanged;
                Subscribe(setupsItemsChanged);
                InvalidateVisual();
            });

            this.GetObservable(AnnotationsProperty).Subscribe(anns =>
            {
                UnsubscribeAnnotations(annotationsChanged);
                annotationsChanged = anns as INotifyCollectionChanged;
                SubscribeAnnotations(annotationsChanged);
                InvalidateVisual();
            });

            PointerMoved += OnPointerMoved;
            PointerExited += OnPointerExited;
            PointerPressed += OnPointerPressed;
        }

        public static readonly StyledProperty<bool> ShowHourLabelsProperty = AvaloniaProperty.Register<TimelineControl, bool>(nameof(ShowHourLabels), true);

        public static readonly StyledProperty<bool> ShowAnnotationsProperty = AvaloniaProperty.Register<TimelineControl, bool>(nameof(ShowAnnotations), true);

        public bool ShowAnnotations
        {
            get => GetValue(ShowAnnotationsProperty);
            set => SetValue(ShowAnnotationsProperty, value);
        }

        public static readonly StyledProperty<bool> EnableTooltipsProperty = AvaloniaProperty.Register<TimelineControl, bool>(nameof(EnableTooltips), false);

        public bool EnableTooltips
        {
            get => GetValue(EnableTooltipsProperty);
            set => SetValue(EnableTooltipsProperty, value);
        }

        public bool ShowHourLabels
        {
            get => GetValue(ShowHourLabelsProperty);
            set => SetValue(ShowHourLabelsProperty, value);
        }

        public static readonly StyledProperty<string> YAxisTopLabelProperty =
            AvaloniaProperty.Register<TimelineControl, string>(nameof(YAxisTopLabel), "Сбой");

        public string YAxisTopLabel
        {
            get => GetValue(YAxisTopLabelProperty);
            set => SetValue(YAxisTopLabelProperty, value);
        }

        public static readonly StyledProperty<string> YAxisBottomLabelProperty =
            AvaloniaProperty.Register<TimelineControl, string>(nameof(YAxisBottomLabel), "OK");

        public string YAxisBottomLabel
        {
            get => GetValue(YAxisBottomLabelProperty);
            set => SetValue(YAxisBottomLabelProperty, value);
        }

        private INotifyCollectionChanged? itemsChanged;
        private INotifyCollectionChanged? repairsItemsChanged;
        private INotifyCollectionChanged? setupsItemsChanged;
        private INotifyCollectionChanged? annotationsChanged;

        private void Subscribe(INotifyCollectionChanged? inc)
        {
            if (inc != null)
                inc.CollectionChanged += OnItemsCollectionChanged;
        }

        private void Unsubscribe(INotifyCollectionChanged? inc)
        {
            if (inc != null)
                inc.CollectionChanged -= OnItemsCollectionChanged;
        }

        private void SubscribeAnnotations(INotifyCollectionChanged? inc)
        {
            if (inc != null)
                inc.CollectionChanged += OnAnnotationsCollectionChanged;
        }

        private void UnsubscribeAnnotations(INotifyCollectionChanged? inc)
        {
            if (inc != null)
                inc.CollectionChanged -= OnAnnotationsCollectionChanged;
        }

        private void OnAnnotationsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateVisual();
        }

        private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            InvalidateVisual();
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!(EnableTooltips || !ShowHourLabels))
            {
                CloseTooltip();
                return;
            }

            var point = e.GetPosition(this);
            if (TryBuildTooltipText(point, out var tooltipText))
            {
                if (!string.Equals(_currentTooltipText, tooltipText, StringComparison.Ordinal))
                {
                    _currentTooltipText = tooltipText;
                    ToolTip.SetTip(this, tooltipText);
                }

                ToolTip.SetIsOpen(this, true);
            }
            else
            {
                CloseTooltip();
            }
        }

        private void OnPointerExited(object? sender, PointerEventArgs e)
        {
            CloseTooltip();
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var command = AnnotationSelectedCommand;
            if (command == null)
                return;

            var point = e.GetPosition(this);
            if (!TryGetAnnotationAt(point, out var selected) || selected == null)
                return;

            if (command.CanExecute(selected))
            {
                command.Execute(selected);
                e.Handled = true;
            }
        }

        private void CloseTooltip()
        {
            ToolTip.SetIsOpen(this, false);
            _currentTooltipText = null;
        }

        private bool TryBuildTooltipText(Point point, out string tooltipText)
        {
            tooltipText = string.Empty;

            var bounds = Bounds;
            double w = bounds.Width;
            double h = bounds.Height;
            if (w <= 0 || h <= 0)
                return false;

            double left = 4;
            double right = 4;
            double top = 4;
            double bottom = ShowHourLabels ? 70 : 4;

            double plotW = Math.Max(1, w - left - right);
            double plotH = Math.Max(1, h - top - bottom);
            double y1 = top + 0.1 * plotH;
            double y0 = top + 0.9 * plotH;

            if (point.X < left || point.X > left + plotW)
                return false;

            var annSource = Annotations as IList;
            if (annSource == null || annSource.Count == 0)
                return false;

            double hour = (point.X - left) / (plotW / 24.0);
            hour = Math.Clamp(hour, 0.0, 24.0);

            var repair = (RepairsItems != null && RepairsItems.Count > 0) ? annSource
                .OfType<Models.Annotation>()
                .Where(a => a.Type == Models.IssueType.Ремонт && hour >= a.StartHour && hour < Math.Max(a.EndHour, a.StartHour + 0.0001))
                .OrderBy(a => a.StartHour)
                .FirstOrDefault() : null;

            var setup = (SetupsItems != null && SetupsItems.Count > 0) ? annSource
                .OfType<Models.Annotation>()
                .Where(a => a.Type == Models.IssueType.Настройка && hour >= a.StartHour && hour < Math.Max(a.EndHour, a.StartHour + 0.0001))
                .OrderBy(a => a.StartHour)
                .FirstOrDefault() : null;

            if (repair == null && setup == null)
                return false;

            var builder = new StringBuilder();
            if (repair != null && setup != null)
            {
                builder.AppendLine("Пересечение событий");
                builder.AppendLine();
            }

            var hasEventBlock = false;

            if (repair != null)
            {
                if (hasEventBlock)
                    AppendTooltipBlockGap(builder);

                AppendAnnotationTooltip(builder, "Ремонт", repair);
                hasEventBlock = true;
            }

            if (setup != null)
            {
                if (hasEventBlock)
                    AppendTooltipBlockGap(builder);

                AppendAnnotationTooltip(builder, "Настройка", setup);
            }

            tooltipText = builder.ToString().TrimEnd();
            return tooltipText.Length > 0;
        }

        private bool TryGetAnnotationAt(Point point, out Models.Annotation? annotation)
        {
            annotation = null;

            var bounds = Bounds;
            double w = bounds.Width;
            double h = bounds.Height;
            if (w <= 0 || h <= 0)
                return false;

            double left = 4;
            double right = 4;
            double top = 4;
            double bottom = ShowHourLabels ? 70 : 4;

            double plotW = Math.Max(1, w - left - right);
            double plotH = Math.Max(1, h - top - bottom);

            if (point.X < left || point.X > left + plotW)
                return false;

            if (point.Y < top || point.Y > top + plotH)
                return false;

            var annSource = Annotations as IList;
            if (annSource == null || annSource.Count == 0)
                return false;

            double hour = (point.X - left) / (plotW / 24.0);
            hour = Math.Clamp(hour, 0.0, 24.0);

            // Find an annotation that covers this hour.
            var matching = annSource.OfType<Models.Annotation>()
                .Where(a => hour >= a.StartHour && hour <= Math.Max(a.EndHour, a.StartHour + 0.1))
                .Where(a => {
                    if (a.Type == Models.IssueType.Ремонт)
                        return RepairsItems != null && RepairsItems.Count > 0;
                    if (a.Type == Models.IssueType.Настройка)
                        return SetupsItems != null && SetupsItems.Count > 0;
                    return true;
                })
                .OrderBy(a => a.StartHour)
                .FirstOrDefault();

            if (matching != null)
            {
                annotation = matching;
                return true;
            }

            return false;
        }

        private static void AppendAnnotationTooltip(StringBuilder builder, string title, Models.Annotation annotation)
        {
            var start = annotation.StartDate.ToString("HH:mm");
            var end = annotation.EndDate.ToString("HH:mm");
            var duration = string.IsNullOrWhiteSpace(annotation.Duration) ? "-" : annotation.Duration.Trim();
            var responsible = string.IsNullOrWhiteSpace(annotation.Responsible) ? "Не назначен" : annotation.Responsible.Trim();
            var description = string.IsNullOrWhiteSpace(annotation.Description) ? "Без описания" : annotation.Description.Trim();

            if (description.Length > 90)
                description = description.Substring(0, 87) + "...";

            builder.AppendLine($"{title} - {start}-{end} - {duration}");
            builder.AppendLine($"Ответственный: {responsible}");
            builder.Append(description);
        }

        private static void AppendTooltipBlockGap(StringBuilder builder)
        {
            builder.AppendLine();
            builder.AppendLine("--------------------");
            builder.AppendLine();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            var items = Items as IList ?? new System.Collections.ArrayList();
            var repairsItems = RepairsItems as IList ?? new System.Collections.ArrayList();
            var setupsItems = SetupsItems as IList ?? new System.Collections.ArrayList();

            var bounds = this.Bounds;
            double w = bounds.Width;
            double h = bounds.Height;
            if (w <= 0 || h <= 0)
                return;

            // margins
            double left = 4;
            double right = 4;
            double top = 4;
            double bottom = ShowHourLabels ? 70 : 4;

            double plotW = Math.Max(1, w - left - right);
            double plotH = Math.Max(1, h - top - bottom);

            // --- estimate annotation space above the plot and shift plot down ---
            var anns = Annotations as IList ?? new System.Collections.ArrayList();
            var annObjs = new System.Collections.Generic.List<Models.Annotation>();
            foreach (var it in anns)
                if (it is Models.Annotation a) annObjs.Add(a);
            annObjs.Sort((a, b) => a.Hour.CompareTo(b.Hour));

            var repairsInProgressDropXs = annObjs
                .Where(a => a.IsInProgress && a.Type == Models.IssueType.Ремонт)
                .Select(a => left + a.EndHour * (plotW / 24.0))
                .ToList();

            var setupsInProgressDropXs = annObjs
                .Where(a => a.IsInProgress && a.Type == Models.IssueType.Настройка)
                .Select(a => left + a.EndHour * (plotW / 24.0))
                .ToList();

            // Set annotation space to 0 to keep the layout compact and clean
            double annSpace = 0;

            // shift plot down
            top += annSpace;
            plotH = Math.Max(1, h - top - bottom);

            // draw horizontal grid lines for 0 and 1 (subtle)
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(50, 203, 213, 225)), 1);
            // y for value 1 (top)
            double y1 = top + 0.1 * plotH;
            // y for value 0 (bottom)
            double y0 = top + 0.9 * plotH;

            context.DrawLine(gridPen, new Point(left, y1), new Point(left + plotW, y1));
            context.DrawLine(gridPen, new Point(left, y0), new Point(left + plotW, y0));

            System.Collections.Generic.List<Point> BuildPoints(IList source)
            {
                var outPts = new System.Collections.Generic.List<Point>();
                if (source == null || source.Count == 0)
                    return outPts;

                if (source[0] is int)
                {
                    double step = plotW / 23.0;
                    for (int i = 0; i < 24; i++)
                    {
                        double x = left + i * step;
                        int val = 0;
                        if (i < source.Count && source[i] is int iv)
                            val = iv;
                        double y = val == 1 ? y1 : y0;
                        outPts.Add(new Point(x, y));
                    }
                    return outPts;
                }

                double tpXScale = plotW / 24.0;
                foreach (var it in source)
                {
                    if (it is Models.TimelinePoint tp)
                    {
                        double x = left + tp.Hour * tpXScale;
                        double y = tp.Value == 1 ? y1 : y0;
                        outPts.Add(new Point(x, y));
                    }
                }

                return outPts;
            }

            var pts = BuildPoints(items).ToArray();
            var repairsPts = BuildPoints(repairsItems).ToArray();
            var setupsPts = BuildPoints(setupsItems).ToArray();

            // draw hourly ticks (scale) - always visible
            double tickTop = top + plotH;
            double tickBottom = tickTop + 6;
            double xScale = plotW / 24.0;
            var tickPen = new Pen(new SolidColorBrush(Color.Parse("#94A3B8")), 1);
            for (int i = 0; i < 24; i++)
            {
                double cx = left + i * xScale;
                context.DrawLine(tickPen, new Point(cx, tickTop), new Point(cx, tickBottom));
            }
            // final tick at 24:00 (render as 23:59 marker)
            double cxEnd = left + plotW; // 24:00
            context.DrawLine(tickPen, new Point(cxEnd, tickTop), new Point(cxEnd, tickBottom));

            // draw vertical grid every 15 minutes (dashed, very subtle)
            var gridDashPen = new Pen(new SolidColorBrush(Color.FromArgb(15, 203, 213, 225)), 1) { DashStyle = new DashStyle(new double[] { 4, 4 }, 0) };
            for (int q = 0; q <= 24 * 4; q++)
            {
                double hour = q * 0.25; // quarter hours
                double gx = left + hour * (plotW / 24.0);
                context.DrawLine(gridDashPen, new Point(gx, top), new Point(gx, top + plotH));
            }

            // draw shift boundary markers (e.g., 08:00 start and 16:30 end)
            double[] shiftHours = new double[] { 8.0, 16.5 };
            var shiftPen = new Pen(new SolidColorBrush(Color.Parse("#818CF8")), 1.2) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
            var labelBrushShift = new SolidColorBrush(Color.Parse("#4F46E5"));
            var tfShift = UiTypeface;
            double labelFont = 11;
            // draw shaded area for the main shift (between first two entries) if available
            if (shiftHours.Length >= 2)
            {
                double sX = left + shiftHours[0] * (plotW / 24.0);
                double eX = left + shiftHours[1] * (plotW / 24.0);
                var fillBrush = new SolidColorBrush(Color.FromArgb(15, 79, 70, 229)); // Soft Indigo tint
                context.FillRectangle(fillBrush, new Rect(sX, top, Math.Max(0, eX - sX), plotH));
            }

            foreach (var sh in shiftHours)
            {
                if (sh < 0 || sh > 24) continue;
                double sx = left + sh * (plotW / 24.0);
                // vertical marker
                context.DrawLine(shiftPen, new Point(sx, top), new Point(sx, top + plotH));
                // label above
                string lbl = TimeSpan.FromHours(sh).Hours.ToString("D2") + ":" + TimeSpan.FromHours(sh).Minutes.ToString("D2");
                var ftLbl = new FormattedText(lbl, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tfShift, labelFont, labelBrushShift);
                double approxW = lbl.Length * labelFont * 0.6;
                context.DrawText(ftLbl, new Point(sx - approxW / 2.0, top - labelFont - 4));
            }

            // draw baseline representing normal (OK) state
            var baselinePen = new Pen(new SolidColorBrush(Color.Parse("#CBD5E1")), 1.5);
            context.DrawLine(baselinePen, new Point(left, y0), new Point(left + plotW, y0));

            // draw y-axis labels (left)
            var labelBrushY = new SolidColorBrush(Color.Parse("#475569"));
            var tfY = UiTypeface;
            double labelFontSize = 11;
            var ftTop = new FormattedText(YAxisTopLabel, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tfY, labelFontSize, labelBrushY);
            var ftBottom = new FormattedText(YAxisBottomLabel, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tfY, labelFontSize, labelBrushY);
            context.DrawText(ftTop, new Point(2, y1 - labelFontSize / 2));
            context.DrawText(ftBottom, new Point(2, y0 - labelFontSize / 2));

            // draw horizontal HH:MM labels if enabled
            if (ShowHourLabels)
            {
                var labelBrush = new SolidColorBrush(Color.Parse("#64748B"));
                double fontSize = 10;
                var tf = UiTypeface;

                for (int i = 0; i < 24; i++)
                {
                    double cx = left + i * xScale;
                    string label = i.ToString("D2") + ":00"; // HH:MM
                    var ft = new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, fontSize, labelBrush);
                    double approxWidth = label.Length * fontSize * 0.6;
                    double drawX = cx - approxWidth / 2.0;
                    double drawY = tickBottom + 4;
                    context.DrawText(ft, new Point(drawX, drawY));
                }
                // label for final minute 23:59 at end
                string endLabel = "23:59";
                var ftEnd = new FormattedText(endLabel, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, fontSize, labelBrush);
                double approxWidthEnd = endLabel.Length * fontSize * 0.6;
                double drawXEnd = cxEnd - approxWidthEnd / 2.0;
                double drawYEnd = tickBottom + 4;
                context.DrawText(ftEnd, new Point(drawXEnd, drawYEnd));
            }

            System.Collections.Generic.List<(double x1, double x2, double y)> BuildHorizontalSegments(Point[] series)
            {
                var segments = new System.Collections.Generic.List<(double x1, double x2, double y)>();
                if (series.Length == 0)
                    return segments;

                for (int i = 0; i < series.Length; i++)
                {
                    double x1 = series[i].X;
                    double x2 = (i < series.Length - 1) ? series[i + 1].X : (left + plotW);
                    double y = series[i].Y;
                    if (x2 > x1 + 0.01)
                        segments.Add((x1, x2, y));
                }

                return segments;
            }



            void DrawSeries(Point[] series, IBrush strokeBrush, IBrush activeAreaBrush, System.Collections.Generic.List<double> inProgressDropXs)
            {
                if (series.Length == 0)
                    return;

                // 1. Draw active area fills
                for (int i = 0; i < series.Length; i++)
                {
                    double xStart = series[i].X;
                    double xEnd = (i < series.Length - 1) ? series[i + 1].X : (left + plotW);
                    double y = series[i].Y;

                    if (Math.Abs(y - y1) < 0.1 && xEnd > xStart)
                    {
                        var areaRect = new Rect(xStart, y1, xEnd - xStart, y0 - y1);
                        context.FillRectangle(activeAreaBrush, areaRect);
                    }
                }

                // 2. Draw outline borders only for active blocks
                var linePen = new Pen(strokeBrush, 1.6) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round };
                for (int i = 0; i < series.Length; i++)
                {
                    double xStart = series[i].X;
                    double xEnd = (i < series.Length - 1) ? series[i + 1].X : (left + plotW);
                    double y = series[i].Y;

                    if (Math.Abs(y - y1) < 0.1)
                    {
                        // Draw top border
                        context.DrawLine(linePen, new Point(xStart, y1), new Point(xEnd, y1));

                        // Draw left border if this is the start of an active block
                        if (i == 0 || Math.Abs(series[i - 1].Y - y0) < 0.1)
                        {
                            context.DrawLine(linePen, new Point(xStart, y0), new Point(xStart, y1));
                        }

                        // Draw right border if this is the end of an active block
                        if (i == series.Length - 1 || Math.Abs(series[i + 1].Y - y0) < 0.1)
                        {
                            context.DrawLine(linePen, new Point(xEnd, y0), new Point(xEnd, y1));
                        }
                    }
                }
            }

            var repairsStroke = new SolidColorBrush(Color.Parse("#EF4444"));

            var repairsAreaGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(90, 239, 68, 68), 0.0),
                    new GradientStop(Color.FromArgb(15, 239, 68, 68), 1.0)
                }
            };

            var setupsStroke = new SolidColorBrush(Color.Parse("#F59E0B"));

            var setupsAreaGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(90, 245, 158, 11), 0.0),
                    new GradientStop(Color.FromArgb(15, 245, 158, 11), 1.0)
                }
            };

            DrawSeries(repairsPts, repairsStroke, repairsAreaGradient, repairsInProgressDropXs);
            DrawSeries(setupsPts, setupsStroke, setupsAreaGradient, setupsInProgressDropXs);

            var repairsHorizontal = BuildHorizontalSegments(repairsPts);
            var setupsHorizontal = BuildHorizontalSegments(setupsPts);

            var mixStroke = new SolidColorBrush(Color.Parse("#F97316"));
            var mixAreaGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(110, 249, 115, 22), 0.0),
                    new GradientStop(Color.FromArgb(18, 249, 115, 22), 1.0)
                }
            };
            var mixPen = new Pen(mixStroke, 2.0) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round };

            foreach (var r in repairsHorizontal)
            {
                foreach (var s in setupsHorizontal)
                {
                    if (Math.Abs(r.y - s.y) > 0.1)
                        continue;

                    double overlapStart = Math.Max(r.x1, s.x1);
                    double overlapEnd = Math.Min(r.x2, s.x2);
                    if (overlapEnd > overlapStart + 0.2)
                    {
                        if (Math.Abs(r.y - y1) < 0.1)
                        {
                            var overlapArea = new Rect(overlapStart, y1, overlapEnd - overlapStart, y0 - y1);
                            context.FillRectangle(mixAreaGradient, overlapArea);
                        }
                        context.DrawLine(mixPen, new Point(overlapStart, r.y), new Point(overlapEnd, r.y));
                    }
                }
            }

            // summary label removed by request
        }
    }
}


