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

        private IBrush GetThemeBrush(string key, string fallbackHex)
        {
            try
            {
                if (this.FindResource(key) is IBrush brush)
                {
                    return brush;
                }
            }
            catch { }

            try
            {
                if (Application.Current != null && Application.Current.FindResource(key) is IBrush appBrush)
                {
                    return appBrush;
                }
            }
            catch { }

            try
            {
                return Brush.Parse(fallbackHex);
            }
            catch
            {
                return Brushes.Gray;
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            var repairsItems = RepairsItems as IList ?? new System.Collections.ArrayList();
            var setupsItems = SetupsItems as IList ?? new System.Collections.ArrayList();

            var bounds = this.Bounds;
            double w = bounds.Width;
            double h = bounds.Height;
            if (w <= 0 || h <= 0)
                return;

            // Margins
            double left = 4;
            double right = 4;
            double top = 4;
            double bottom = ShowHourLabels ? 22 : 4;

            double plotW = Math.Max(1, w - left - right);
            double plotH = Math.Max(1, h - top - bottom);

            // Retrieve annotations to determine "IsInProgress" status
            var anns = Annotations as IList ?? new System.Collections.ArrayList();
            var annObjs = new System.Collections.Generic.List<Models.Annotation>();
            foreach (var it in anns)
            {
                if (it is Models.Annotation a) 
                    annObjs.Add(a);
            }

            // Track calculations for the Gantt bar
            double trackHeight = Math.Min(24, Math.Max(12, plotH));
            double trackY = top + (plotH - trackHeight) / 2.0;

            // We define y1 and y0 to reuse the BuildPoints helper logic.
            double y1 = 1;
            double y0 = 0;

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

            var repairsPts = BuildPoints(repairsItems).ToArray();
            var setupsPts = BuildPoints(setupsItems).ToArray();

            System.Collections.Generic.List<(double start, double end)> ExtractActiveIntervals(Point[] series)
            {
                var list = new System.Collections.Generic.List<(double start, double end)>();
                for (int i = 0; i < series.Length; i++)
                {
                    double xStart = series[i].X;
                    double xEnd = (i < series.Length - 1) ? series[i + 1].X : (left + plotW);
                    double y = series[i].Y;
                    if (Math.Abs(y - y1) < 0.1 && xEnd > xStart + 0.01)
                    {
                        list.Add((xStart, xEnd));
                    }
                }
                return list;
            }

            var repairs = ExtractActiveIntervals(repairsPts);
            var setups = ExtractActiveIntervals(setupsPts);

            // Calculate overlap intervals
            var overlaps = new System.Collections.Generic.List<(double start, double end)>();
            foreach (var r in repairs)
            {
                foreach (var s in setups)
                {
                    double oStart = Math.Max(r.start, s.start);
                    double oEnd = Math.Min(r.end, s.end);
                    if (oEnd > oStart + 0.01)
                    {
                        overlaps.Add((oStart, oEnd));
                    }
                }
            }

            System.Collections.Generic.List<(double start, double end)> SubtractIntervals(System.Collections.Generic.List<(double start, double end)> source, System.Collections.Generic.List<(double start, double end)> toSubtract)
            {
                var result = new System.Collections.Generic.List<(double start, double end)>(source);
                foreach (var sub in toSubtract)
                {
                    var nextResult = new System.Collections.Generic.List<(double start, double end)>();
                    foreach (var src in result)
                    {
                        if (src.end <= sub.start || src.start >= sub.end)
                        {
                            nextResult.Add(src);
                        }
                        else
                        {
                            if (src.start < sub.start)
                            {
                                nextResult.Add((src.start, sub.start));
                            }
                            if (src.end > sub.end)
                            {
                                nextResult.Add((sub.end, src.end));
                            }
                        }
                    }
                    result = nextResult;
                }
                return result;
            }

            var repairsOnly = SubtractIntervals(repairs, overlaps);
            var setupsOnly = SubtractIntervals(setups, overlaps);

            var repairsStroke = GetThemeBrush("DangerBrush", "#EF4444");
            var setupsStroke = GetThemeBrush("WarningBrush", "#F59E0B");
            var stripeBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));

            bool IsRepairInProgress(double start, double end)
            {
                double scaleX = plotW / 24.0;
                foreach (var a in annObjs)
                {
                    if (a.Type == Models.IssueType.Ремонт && a.IsInProgress)
                    {
                        double aStart = left + a.StartHour * scaleX;
                        double aEnd = left + a.EndHour * scaleX;
                        if (start >= aStart - 1.0 && end <= aEnd + 1.0)
                            return true;
                    }
                }
                return false;
            }

            bool IsSetupInProgress(double start, double end)
            {
                double scaleX = plotW / 24.0;
                foreach (var a in annObjs)
                {
                    if (a.Type == Models.IssueType.Настройка && a.IsInProgress)
                    {
                        double aStart = left + a.StartHour * scaleX;
                        double aEnd = left + a.EndHour * scaleX;
                        if (start >= aStart - 1.0 && end <= aEnd + 1.0)
                            return true;
                    }
                }
                return false;
            }

            void DrawHatchPattern(DrawingContext ctx, Rect rect, IBrush hatchBrush)
            {
                var pen = new Pen(hatchBrush, 1.8);
                double spacing = 8.0;
                double xStart = rect.X;
                double xEnd = rect.X + rect.Width;
                double yTop = rect.Y;
                double height = rect.Height;

                for (double hx = xStart - height; hx < xEnd; hx += spacing)
                {
                    double lineStart = Math.Max(xStart, hx);
                    double lineEnd = Math.Min(xEnd, hx + height);
                    if (lineEnd > lineStart + 0.1)
                    {
                        double yStartOffset = lineStart - hx;
                        double yEndOffset = lineEnd - hx;
                        ctx.DrawLine(pen,
                            new Point(lineStart, yTop + yStartOffset),
                            new Point(lineEnd, yTop + yEndOffset)
                        );
                    }
                }
            }

            // Calculate shift boundary coordinates
            double[] shiftHours = new double[] { 8.0, 16.5 };
            double sX = left + shiftHours[0] * (plotW / 24.0);
            double eX = left + shiftHours[1] * (plotW / 24.0);

            // Push clip geometry to round all track elements
            var clipGeom = new RectangleGeometry(new Rect(left, trackY, plotW, trackHeight), 4, 4);
            using (context.PushGeometryClip(clipGeom))
            {
                // 1. Draw base track
                var okBrush = GetThemeBrush("BorderSubtleBrush", "#EEF1F6");
                var trackRect = new Rect(left, trackY, plotW, trackHeight);
                context.FillRectangle(okBrush, trackRect);

                // 2. Draw shift boundary soft fill inside the track
                var shiftAreaBrush = new SolidColorBrush(Color.FromArgb(15, 79, 70, 229)); // Soft Indigo tint
                context.FillRectangle(shiftAreaBrush, new Rect(sX, trackY, Math.Max(0, eX - sX), trackHeight));

                // 3. Draw Repair-only segments
                foreach (var r in repairsOnly)
                {
                    var rect = new Rect(r.start, trackY, r.end - r.start, trackHeight);
                    context.FillRectangle(repairsStroke, rect);
                    if (IsRepairInProgress(r.start, r.end))
                    {
                        DrawHatchPattern(context, rect, stripeBrush);
                    }
                }

                // 4. Draw Setup-only segments
                foreach (var s in setupsOnly)
                {
                    var rect = new Rect(s.start, trackY, s.end - s.start, trackHeight);
                    context.FillRectangle(setupsStroke, rect);
                    if (IsSetupInProgress(s.start, s.end))
                    {
                        DrawHatchPattern(context, rect, stripeBrush);
                    }
                }

                // 5. Draw Overlap segments (split vertically)
                foreach (var o in overlaps)
                {
                    double midY = trackY + trackHeight / 2.0;

                    // Top half: Repair
                    var topRect = new Rect(o.start, trackY, o.end - o.start, trackHeight / 2.0);
                    context.FillRectangle(repairsStroke, topRect);
                    if (IsRepairInProgress(o.start, o.end))
                    {
                        DrawHatchPattern(context, topRect, stripeBrush);
                    }

                    // Bottom half: Setup
                    var bottomRect = new Rect(o.start, midY, o.end - o.start, trackHeight / 2.0);
                    context.FillRectangle(setupsStroke, bottomRect);
                    if (IsSetupInProgress(o.start, o.end))
                    {
                        DrawHatchPattern(context, bottomRect, stripeBrush);
                    }
                }
            }

            // Draw vertical shift boundary lines passing behind/through the track
            var shiftPen = new Pen(new SolidColorBrush(Color.Parse("#818CF8")), 1.0) { DashStyle = new DashStyle(new double[] { 3, 3 }, 0) };
            var labelBrushShift = new SolidColorBrush(Color.Parse("#4F46E5"));
            var tfShift = UiTypeface;
            double labelFont = 9;

            foreach (var sh in shiftHours)
            {
                double sx = left + sh * (plotW / 24.0);
                context.DrawLine(shiftPen, new Point(sx, trackY - 2), new Point(sx, trackY + trackHeight + 2));

                if (plotH > 35)
                {
                    string lbl = TimeSpan.FromHours(sh).Hours.ToString("D2") + ":" + TimeSpan.FromHours(sh).Minutes.ToString("D2");
                    var ftLbl = new FormattedText(lbl, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tfShift, labelFont, labelBrushShift);
                    double approxW = lbl.Length * labelFont * 0.6;
                    context.DrawText(ftLbl, new Point(sx - approxW / 2.0, trackY - labelFont - 3));
                }
            }

            // Draw Time Scale and Hour Labels if enabled
            if (ShowHourLabels)
            {
                double tickTop = trackY + trackHeight + 2;
                double tickBottom = tickTop + 4;
                var tickPen = new Pen(new SolidColorBrush(Color.Parse("#94A3B8")), 1);
                double xScale = plotW / 24.0;

                for (int i = 0; i <= 24; i++)
                {
                    double cx = left + i * xScale;
                    context.DrawLine(tickPen, new Point(cx, tickTop), new Point(cx, tickBottom));
                }

                var labelBrush = new SolidColorBrush(Color.Parse("#64748B"));
                double fontSize = 10;
                var tf = UiTypeface;

                for (int i = 0; i < 24; i += 2) // Label every 2 hours to keep it neat
                {
                    double cx = left + i * xScale;
                    string label = i.ToString("D2") + ":00";
                    var ft = new FormattedText(label, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, fontSize, labelBrush);
                    double approxWidth = label.Length * fontSize * 0.6;
                    context.DrawText(ft, new Point(cx - approxWidth / 2.0, tickBottom + 2));
                }

                string endLabel = "24:00";
                var ftEnd = new FormattedText(endLabel, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, fontSize, labelBrush);
                double approxWidthEnd = endLabel.Length * fontSize * 0.6;
                context.DrawText(ftEnd, new Point(left + plotW - approxWidthEnd / 2.0, tickBottom + 2));
            }
        }
    }
}


