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
            Models.Annotation? selected = null;
            if (TryGetAnnotationAt(point, out var found))
                selected = found;

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

            if (point.X < left || point.X > left + plotW || point.Y < y1 || point.Y > y0)
                return false;

            var annSource = Annotations as IList;
            if (annSource == null || annSource.Count == 0)
                return false;

            double hour = (point.X - left) / (plotW / 24.0);
            hour = Math.Clamp(hour, 0.0, 24.0);

            var repair = annSource
                .OfType<Models.Annotation>()
                .Where(a => a.Type == Models.IssueType.Ремонт && hour >= a.StartHour && hour < Math.Max(a.EndHour, a.StartHour + 0.0001))
                .OrderBy(a => a.StartHour)
                .FirstOrDefault();

            var setup = annSource
                .OfType<Models.Annotation>()
                .Where(a => a.Type == Models.IssueType.Настройка && hour >= a.StartHour && hour < Math.Max(a.EndHour, a.StartHour + 0.0001))
                .OrderBy(a => a.StartHour)
                .FirstOrDefault();

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
            double y1 = top + 0.1 * plotH;
            double y0 = top + 0.9 * plotH;

            if (point.X < left || point.X > left + plotW || point.Y < y1 || point.Y > y0)
                return false;

            var annSource = Annotations as IList;
            if (annSource == null || annSource.Count == 0)
                return false;

            double hour = (point.X - left) / (plotW / 24.0);
            hour = Math.Clamp(hour, 0.0, 24.0);

            var active = annSource
                .OfType<Models.Annotation>()
                .Where(a => hour >= a.StartHour && hour < Math.Max(a.EndHour, a.StartHour + 0.0001))
                .OrderBy(a => Math.Abs(hour - ((a.StartHour + a.EndHour) / 2.0)))
                .ThenByDescending(a =>
                {
                    if (TimeSpan.TryParse(a.Duration, out var parsed))
                        return parsed.TotalMinutes;
                    return 0.0;
                })
                .FirstOrDefault();

            if (active != null)
            {
                annotation = active;
                return true;
            }

            var nearest = annSource
                .OfType<Models.Annotation>()
                .Select(a => new { Annotation = a, Dist = Math.Abs(hour - a.StartHour) })
                .OrderBy(x => x.Dist)
                .FirstOrDefault();

            if (nearest != null && nearest.Dist <= 0.5)
            {
                annotation = nearest.Annotation;
                return true;
            }

            return false;
        }

        private static void AppendAnnotationTooltip(StringBuilder builder, string title, Models.Annotation annotation)
        {
            var start = annotation.StartDate.ToString("HH:mm");
            var end = annotation.EndDate.ToString("HH:mm");
            var duration = string.IsNullOrWhiteSpace(annotation.Duration) ? "—" : annotation.Duration.Trim();
            var responsible = string.IsNullOrWhiteSpace(annotation.Responsible) ? "Не назначен" : annotation.Responsible.Trim();
            var description = string.IsNullOrWhiteSpace(annotation.Description) ? "Без описания" : annotation.Description.Trim();

            if (description.Length > 90)
                description = description.Substring(0, 87) + "...";

            builder.AppendLine($"{title}  •  {start}–{end}  •  {duration}");
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

            // don't return when there is no data: still draw scale/ticks

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
            var annTypeface = UiTypeface;
            var annTitleTypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.SemiBold);
            double annFont = 12;
            double gap = 6;

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

            // precompute counts and durations of issue types for summary
            int repairCount = 0;
            int setupCount = 0;
            double repairMinutesSum = 0.0;
            double setupMinutesSum = 0.0;
            foreach (var a in annObjs)
            {
                try
                {
                    TimeSpan dur = TimeSpan.Zero;
                    try { TimeSpan.TryParse(a.Duration, out dur); } catch { dur = TimeSpan.Zero; }
                    if (a.Type == Models.IssueType.Ремонт)
                    {
                        repairCount++;
                        repairMinutesSum += dur.TotalMinutes;
                    }
                    else if (a.Type == Models.IssueType.Настройка)
                    {
                        setupCount++;
                        setupMinutesSum += dur.TotalMinutes;
                    }
                }
                catch { }
            }

            int annCount = annObjs.Count;
            double[] annWidths = new double[annCount];
            double[] annHeights = new double[annCount];
            double[] annCenters = new double[annCount];

            for (int i = 0; i < annCount; i++)
            {
                var a = annObjs[i];
                string l1 = a.Description ?? string.Empty;
                string l2 = a.Responsible ?? string.Empty;
                string l3 = a.Duration ?? string.Empty;
                int maxLen = Math.Max(l1.Length, Math.Max(l2.Length, l3.Length));
                annWidths[i] = Math.Max(140, maxLen * (annFont * 0.52));
                annHeights[i] = annFont * 3 + 16;
                annCenters[i] = left + a.Hour * (plotW / 24.0);
            }

            // decide rows needed: 1 if no overlaps, else 2 (chess)
            bool annSingleRow = true;
            for (int i = 0; i < annCount - 1; i++)
            {
                double need = (annWidths[i] + annWidths[i + 1]) / 2.0 + gap;
                if (annCenters[i + 1] - annCenters[i] < need)
                {
                    annSingleRow = false;
                    break;
                }
            }

            int annRows = annCount == 0 ? 0 : (annSingleRow ? 1 : 2);
            double maxAnnH = annCount > 0 ? annHeights.Max() : 0;
            double annSpace = (ShowHourLabels && ShowAnnotations) ? annRows * (maxAnnH + gap) + 28 : 0;

            // shift plot down
            top += annSpace;
            plotH = Math.Max(1, h - top - bottom);

            // draw horizontal grid lines for 0 and 1 (subtle)
            var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 200, 200, 200)), 1);
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
            var tickPen = new Pen(Brushes.Gray, 1);
            for (int i = 0; i < 24; i++)
            {
                double cx = left + i * xScale;
                context.DrawLine(tickPen, new Point(cx, tickTop), new Point(cx, tickBottom));
            }
            // final tick at 24:00 (render as 23:59 marker)
            double cxEnd = left + plotW; // 24:00
            context.DrawLine(tickPen, new Point(cxEnd, tickTop), new Point(cxEnd, tickBottom));

            // draw vertical grid every 15 minutes (dashed, very subtle)
            var gridDashPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 200, 200, 200)), 1) { DashStyle = new DashStyle(new double[] { 4, 4 }, 0) };
            for (int q = 0; q <= 24 * 4; q++)
            {
                double hour = q * 0.25; // quarter hours
                double gx = left + hour * (plotW / 24.0);
                context.DrawLine(gridDashPen, new Point(gx, top), new Point(gx, top + plotH));
            }

            // draw shift boundary markers (e.g., 08:00 start and 16:30 end)
            double[] shiftHours = new double[] { 8.0, 16.5 };
            var shiftPen = new Pen(new SolidColorBrush(Color.FromArgb(130, 46, 125, 50)), 1.6);
            var labelBrushShift = new SolidColorBrush(Color.FromArgb(150, 46, 125, 50));
            var tfShift = UiTypeface;
            double labelFont = 11;
            // draw shaded area for the main shift (between first two entries) if available
            if (shiftHours.Length >= 2)
            {
                double sX = left + shiftHours[0] * (plotW / 24.0);
                double eX = left + shiftHours[1] * (plotW / 24.0);
                var fillBrush = new SolidColorBrush(Color.FromArgb(35, 76, 175, 80));
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

            if (ShowHourLabels && ShowAnnotations)
            {
                // draw annotations (markers + small text above graph)
                var connectorPen = new Pen(Brushes.Gray, 1) { DashStyle = new DashStyle(new double[] { 1, 2 }, 0) };
                var placedRects = new System.Collections.Generic.List<Rect>();

                for (int i = 0; i < annCount; i++)
                {
                    var a = annObjs[i];
                    double ax = annCenters[i];

                    Color typeColor = Color.Parse("#E65100");
                    try
                    {
                        if (a.Type == Models.IssueType.Ремонт)
                            typeColor = Color.Parse("#C62828");
                        else if (a.Type == Models.IssueType.Настройка)
                            typeColor = Color.Parse("#F9A825");
                    }
                    catch { }

                    var markerBrush = new SolidColorBrush(typeColor);
                    var marker = new Rect(ax - 6, top - 13, 12, 12);
                    var eg = new EllipseGeometry(new Rect(marker.X, marker.Y, marker.Width, marker.Height));
                    context.DrawGeometry(markerBrush, new Pen(Brushes.White, 1), eg);

                    string rawDescription = string.IsNullOrWhiteSpace(a.Description) ? "-" : a.Description.Trim();
                    string rawResponsible = string.IsNullOrWhiteSpace(a.Responsible) ? "-" : a.Responsible.Trim();
                    string rawDuration = string.IsNullOrWhiteSpace(a.Duration) ? "-" : a.Duration.Trim();

                    int maxLen2 = 52;
                    if (rawDescription.Length > maxLen2) rawDescription = rawDescription.Substring(0, maxLen2 - 3) + "...";
                    if (rawResponsible.Length > maxLen2) rawResponsible = rawResponsible.Substring(0, maxLen2 - 3) + "...";

                    string line1 = rawDescription;
                    string line2 = "Ответственный: " + rawResponsible;
                    string line3 = "Длительность: " + rawDuration;

                    // recalc width/height based on actual text lengths to avoid overflow
                    double wRect = annWidths[i];
                    double hRect = annHeights[i];
                    // approximate text widths
                    double ft1w = Math.Max(0, line1.Length * (annFont * 0.6));
                    double ft2w = Math.Max(0, line2.Length * (annFont * 0.6));
                    double ft3w = Math.Max(0, line3.Length * (annFont * 0.6));
                    double contentW = Math.Max(ft1w, Math.Max(ft2w, ft3w));
                    wRect = Math.Max(wRect, contentW + 12); // padding

                    double xRect = ax - wRect / 2.0;
                    double minX = left;
                    double maxX = left + plotW - wRect;
                    xRect = Math.Max(minX, Math.Min(xRect, maxX));

                    double yRect = annSingleRow ? (top - 18 - hRect - 4) : (top - 18 - hRect - 4 - (i % 2) * (hRect + gap));

                    // simple collision avoidance: nudge horizontally if intersects previous
                    var candidate = new Rect(xRect, yRect, wRect, hRect);
                    int tries = 0;
                    while (tries < 10)
                    {
                        bool coll = false;
                        foreach (var pr in placedRects)
                        {
                            if (pr.Intersects(candidate))
                            {
                                coll = true;
                                // try move right
                                double nx = pr.X + pr.Width + gap;
                                if (nx <= maxX) candidate = new Rect(nx, candidate.Y, candidate.Width, candidate.Height);
                                else
                                {
                                    // move leftmost
                                    candidate = new Rect(minX, candidate.Y, candidate.Width, candidate.Height);
                                    // try move up one extra row if chess mode
                                    if (!annSingleRow)
                                        candidate = new Rect(candidate.X, candidate.Y - (hRect + gap), candidate.Width, candidate.Height);
                                }
                                break;
                            }
                        }
                        if (!coll) break;
                        tries++;
                    }

                    placedRects.Add(candidate);

                    var textRect = candidate;
                    var startPoint = new Point(ax, top);
                    var endPoint = new Point(textRect.X + textRect.Width / 2.0, textRect.Y + textRect.Height);
                    context.DrawLine(connectorPen, startPoint, endPoint);

                    // ensure the annotation box is wide enough for text and has padding
                    double annFt1w = Math.Max(0, line1.Length * (annFont * 0.6));
                    double annFt2w = Math.Max(0, line2.Length * (annFont * 0.6));
                    double annFt3w = Math.Max(0, line3.Length * (annFont * 0.6));
                    double annContentW = Math.Max(annFt1w, Math.Max(annFt2w, annFt3w));
                    wRect = Math.Max(wRect, annContentW + 12); // padding
                    // clamp width so it doesn't overflow plot area
                    wRect = Math.Min(wRect, plotW - 8);
                    textRect = new Rect(Math.Max(left, Math.Min(left + plotW - wRect, textRect.X)), textRect.Y, wRect, hRect);

                    // draw rectangular annotation box with solid background and thin border
                    var fillBrush2 = new SolidColorBrush(Color.FromArgb(248, 255, 255, 255));
                    var borderPen2 = new Pen(new SolidColorBrush(Color.FromArgb(220, typeColor.R, typeColor.G, typeColor.B)), 1.2);
                    var roundedTextRect = new RoundedRect(textRect, 6);
                    context.DrawRectangle(fillBrush2, borderPen2, roundedTextRect);

                    var ft1 = new FormattedText(line1, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, annTitleTypeface, annFont, Brushes.Black);
                    var ft2 = new FormattedText(line2, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, annTypeface, annFont - 0.4, Brushes.Black);
                    var ft3 = new FormattedText(line3, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, annTypeface, annFont - 0.4, Brushes.Black);
                    context.DrawText(ft1, new Point(textRect.X + 6, textRect.Y + 4));
                    context.DrawText(ft2, new Point(textRect.X + 6, textRect.Y + 4 + annFont + 3));
                    context.DrawText(ft3, new Point(textRect.X + 6, textRect.Y + 4 + 2 * (annFont + 3)));
                }
            }

            // tooltips removed per request (annotations show multiline text directly)

            // draw y-axis labels (left)
            var labelBrushY = Brushes.Black;
            var tfY = UiTypeface;
            double labelFontSize = 12;
            var ftTop = new FormattedText("Сбой", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tfY, labelFontSize, labelBrushY);
            var ftBottom = new FormattedText("OK", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tfY, labelFontSize, labelBrushY);
            context.DrawText(ftTop, new Point(2, y1 - labelFontSize / 2));
            context.DrawText(ftBottom, new Point(2, y0 - labelFontSize / 2));

            // draw horizontal HH:MM labels if enabled
            if (ShowHourLabels)
            {
                var labelBrush = Brushes.Black;
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

            static bool IsNearInProgressDrop(double x, System.Collections.Generic.List<double> markers)
            {
                if (markers == null || markers.Count == 0)
                    return false;

                const double tolerancePx = 1.0;
                return markers.Any(mx => Math.Abs(mx - x) <= tolerancePx);
            }

            void DrawZigzagTransition(double x, double yStart, double yEnd, Pen pen)
            {
                const double ampX = 2.0;
                const int segments = 8;

                var prev = new Point(x, yStart);
                for (int i = 1; i <= segments; i++)
                {
                    var t = i / (double)segments;
                    var yy = yStart + (yEnd - yStart) * t;
                    var xx = x + ((i % 2 == 0) ? ampX : -ampX);
                    var current = new Point(xx, yy);
                    context.DrawLine(pen, prev, current);
                    prev = current;
                }

                context.DrawLine(pen, prev, new Point(x, yEnd));
            }

            void DrawSeries(Point[] series, IBrush strokeBrush, IBrush activeAreaBrush, System.Collections.Generic.List<double> inProgressDropXs)
            {
                if (series.Length == 0)
                    return;

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

                var linePen = new Pen(strokeBrush, 1.4) { LineJoin = PenLineJoin.Round, LineCap = PenLineCap.Round };
                for (int i = 0; i < series.Length; i++)
                {
                    double xStart = series[i].X;
                    double xEnd = (i < series.Length - 1) ? series[i + 1].X : (left + plotW);
                    double y = series[i].Y;
                    context.DrawLine(linePen, new Point(xStart, y), new Point(xEnd, y));

                    if (i < series.Length - 1)
                    {
                        double nextY = series[i + 1].Y;
                        if (Math.Abs(nextY - y) > 0.1)
                        {
                            var isDropFromOneToZero = Math.Abs(y - y1) < 0.1 && Math.Abs(nextY - y0) < 0.1;
                            if (isDropFromOneToZero && IsNearInProgressDrop(xEnd, inProgressDropXs))
                                DrawZigzagTransition(xEnd, y, nextY, linePen);
                            else
                                context.DrawLine(linePen, new Point(xEnd, y), new Point(xEnd, nextY));
                        }
                    }
                }
            }

            var repairsStroke = new SolidColorBrush(Color.Parse("#D32F2F"));

            var repairsAreaGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(110, 211, 47, 47), 0.0),
                    new GradientStop(Color.FromArgb(15, 211, 47, 47), 1.0)
                }
            };

            var setupsStroke = new SolidColorBrush(Color.Parse("#FBC02D"));

            var setupsAreaGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(110, 251, 192, 45), 0.0),
                    new GradientStop(Color.FromArgb(12, 251, 192, 45), 1.0)
                }
            };

            DrawSeries(repairsPts, repairsStroke, repairsAreaGradient, repairsInProgressDropXs);
            DrawSeries(setupsPts, setupsStroke, setupsAreaGradient, setupsInProgressDropXs);

            var repairsHorizontal = BuildHorizontalSegments(repairsPts);
            var setupsHorizontal = BuildHorizontalSegments(setupsPts);

            var mixStroke = new SolidColorBrush(Color.Parse("#FF8F00"));
            var mixAreaGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(130, 255, 143, 0), 0.0),
                    new GradientStop(Color.FromArgb(18, 255, 143, 0), 1.0)
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
