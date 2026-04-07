using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace EquipmentFailureAnalysis.Views
{
    public class TimelineControl : Control
    {
        public static readonly StyledProperty<IList?> ItemsProperty = AvaloniaProperty.Register<TimelineControl, IList?>(nameof(Items));

        public IList? Items
        {
            get => GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public static readonly StyledProperty<IList?> AnnotationsProperty = AvaloniaProperty.Register<TimelineControl, IList?>(nameof(Annotations));

        public IList? Annotations
        {
            get => GetValue(AnnotationsProperty);
            set => SetValue(AnnotationsProperty, value);
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

            this.GetObservable(AnnotationsProperty).Subscribe(anns =>
            {
                UnsubscribeAnnotations(annotationsChanged);
                annotationsChanged = anns as INotifyCollectionChanged;
                SubscribeAnnotations(annotationsChanged);
                InvalidateVisual();
            });

            // pointer hover tooltip removed; annotations drawn inline
        }

        public static readonly StyledProperty<bool> ShowHourLabelsProperty = AvaloniaProperty.Register<TimelineControl, bool>(nameof(ShowHourLabels), true);

        public bool ShowHourLabels
        {
            get => GetValue(ShowHourLabelsProperty);
            set => SetValue(ShowHourLabelsProperty, value);
        }

        private INotifyCollectionChanged? itemsChanged;
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

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            var items = Items as IList ?? new System.Collections.ArrayList();

            int count = items.Count;
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
            var annTypeface = new Typeface("Segoe UI");
            double annFont = 11;
            double gap = 6;

            var annObjs = new System.Collections.Generic.List<Models.Annotation>();
            foreach (var it in anns)
                if (it is Models.Annotation a) annObjs.Add(a);
            annObjs.Sort((a, b) => a.Hour.CompareTo(b.Hour));

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
                annWidths[i] = Math.Max(40, maxLen * (annFont * 0.5));
                annHeights[i] = annFont * 3 + 8;
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
            double maxAnnH = annCount > 0 ? System.Linq.Enumerable.Max(annHeights) : 0;
            double annSpace = annRows * (maxAnnH + gap) + 28; // extra for connector/marker (raised)

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

            // build point list; support two modes:
            // - IList<int> of length 24 -> hourly values at integer hours
            // - IList<TimelinePoint> -> fractional hour points
            System.Collections.Generic.List<Point> ptsList = new System.Collections.Generic.List<Point>();

            // detect item type
            if (items is System.Collections.IList list && list.Count > 0 && list[0] is int)
            {
                double step = plotW / 23.0;
                for (int i = 0; i < 24; i++)
                {
                    double x = left + i * step;
                    int val = 0;
                    if (i < items.Count && items[i] is int iv)
                        val = iv;
                    double y = val == 1 ? y1 : y0;
                    ptsList.Add(new Point(x, y));
                }
            }
            else
            {
                // assume TimelinePoint objects
                var tpList = new System.Collections.Generic.List<Models.TimelinePoint>();
                foreach (var it in items)
                {
                    if (it is Models.TimelinePoint tp)
                        tpList.Add(tp);
                }
                if (tpList.Count == 0)
                    return;

                // map hours (0..24) to x coordinates
                double tpXScale = plotW / 24.0;
                foreach (var tp in tpList)
                {
                    double x = left + tp.Hour * tpXScale;
                    double y = tp.Value == 1 ? y1 : y0;
                    ptsList.Add(new Point(x, y));
                }
            }

            var pts = ptsList.ToArray();

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
            var shiftPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 220, 57, 57)), 1.6);
            var labelBrushShift = new SolidColorBrush(Color.FromArgb(200, 220, 57, 57));
            var tfShift = new Typeface("Segoe UI");
            double labelFont = 11;
            // draw shaded area for the main shift (between first two entries) if available
            if (shiftHours.Length >= 2)
            {
                double sX = left + shiftHours[0] * (plotW / 24.0);
                double eX = left + shiftHours[1] * (plotW / 24.0);
                var fillBrush = new SolidColorBrush(Color.FromArgb(30, 220, 57, 57));
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

            // draw annotations (markers + small text above graph)
            var connectorPen = new Pen(Brushes.Gray, 1) { DashStyle = new DashStyle(new double[] { 1, 2 }, 0) };
            var placedRects = new System.Collections.Generic.List<Rect>();

            for (int i = 0; i < annCount; i++)
            {
                var a = annObjs[i];
                double ax = annCenters[i];

                // draw marker: color depends on issue type (Ремонт = red, Настройка = yellow)
                    var triBrush = Brushes.OrangeRed;
                    try
                    {
                        if (a.Type == Models.IssueType.Ремонт)
                            triBrush = Brushes.Red;
                        else if (a.Type == Models.IssueType.Настройка)
                            triBrush = Brushes.Yellow;
                    }
                    catch
                    {
                        // fallback to default
                        triBrush = Brushes.OrangeRed;
                    }
                    var marker = new Rect(ax - 5, top - 12, 10, 10);
                    // draw filled circle marker using EllipseGeometry
                    var eg = new EllipseGeometry(new Rect(marker.X, marker.Y, marker.Width, marker.Height));
                    context.DrawGeometry(triBrush, null, eg);

                string line1 = a.Description ?? string.Empty;
                string line2 = a.Responsible ?? string.Empty;
                string line3 = a.Duration ?? string.Empty;
                int maxLen2 = 80;
                if (line1.Length > maxLen2) line1 = line1.Substring(0, maxLen2 - 3) + "...";
                if (line2.Length > maxLen2) line2 = line2.Substring(0, maxLen2 - 3) + "...";
                if (line3.Length > maxLen2) line3 = line3.Substring(0, maxLen2 - 3) + "...";

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
                var rx = textRect.X;
                var ry = textRect.Y;
                var rw2 = textRect.Width;
                var rh2 = textRect.Height;
                var fillBrush2 = new SolidColorBrush(Color.FromArgb(255, 255, 249, 220));
                var borderPen2 = new Pen(new SolidColorBrush(Color.FromArgb(200, 170, 170, 170)), 1);
                context.FillRectangle(fillBrush2, textRect);
                context.DrawRectangle(borderPen2, textRect);

                var ft1 = new FormattedText(line1, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, annTypeface, annFont, Brushes.DarkRed);
                var ft2 = new FormattedText(line2, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, annTypeface, annFont, Brushes.DarkSlateGray);
                var ft3 = new FormattedText(line3, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, annTypeface, annFont, Brushes.DarkSlateGray);
                context.DrawText(ft1, new Point(textRect.X + 4, textRect.Y + 2));
                context.DrawText(ft2, new Point(textRect.X + 4, textRect.Y + 2 + annFont + 2));
                context.DrawText(ft3, new Point(textRect.X + 4, textRect.Y + 2 + 2 * (annFont + 2)));
            }

            // tooltips removed per request (annotations show multiline text directly)

            // draw y-axis labels (left)
            var labelBrushY = Brushes.Black;
            var tfY = new Typeface("Segoe UI");
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
                var tf = new Typeface("Segoe UI");

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

            // compute downtime summary (minutes) from the step segments
            double downtimeMinutes = 0.0;
            double xToHours = 24.0 / plotW; // hours per pixel factor inverted
            for (int i = 0; i < pts.Length; i++)
            {
                double xStart = pts[i].X;
                double xEnd = (i < pts.Length - 1) ? pts[i + 1].X : (left + plotW);
                double y = pts[i].Y;
                // duration in hours for this segment
                double durHours = (xEnd - xStart) / (plotW / 24.0);
                if (Math.Abs(y - y1) < 0.1)
                    downtimeMinutes += durHours * 60.0;

                // draw horizontal segment (primary color)
                var linePen = new Pen(new SolidColorBrush(Color.Parse("#1976D2")), 2) { LineJoin = PenLineJoin.Round };
                context.DrawLine(linePen, new Point(xStart, y), new Point(xEnd, y));

                // draw gradient fill under segment
                var grad = new LinearGradientBrush();
                grad.StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative);
                grad.EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative);
                grad.GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(160, 25, 118, 210), 0.0),
                    new GradientStop(Color.FromArgb(40, 25, 118, 210), 0.6),
                    new GradientStop(Color.FromArgb(0, 25, 118, 210), 1.0)
                };
                var rect = new Rect(xStart, Math.Min(y, y0), Math.Max(1, xEnd - xStart), Math.Abs(y0 - y));
                context.FillRectangle(grad, rect);

                // draw vertical transition at the end of the segment if next value differs
                if (i < pts.Length - 1)
                {
                    double nextY = pts[i + 1].Y;
                    if (Math.Abs(nextY - y) > 0.1)
                    {
                        context.DrawLine(linePen, new Point(xEnd, y), new Point(xEnd, nextY));
                    }
                }
            }

            // draw repair/setup ratio summary label (top-right inside plot)
            // compute repair/setup percentages weighted by downtime minutes (fallback to counts if durations absent)
            double totalMinutesForTypes = repairMinutesSum + setupMinutesSum;
            double repairPct = 0.0;
            double setupPct = 0.0;
            if (totalMinutesForTypes > 0.5)
            {
                repairPct = (repairMinutesSum / totalMinutesForTypes) * 100.0;
                setupPct = (setupMinutesSum / totalMinutesForTypes) * 100.0;
            }
            else
            {
                double totalCount = repairCount + setupCount;
                if (totalCount > 0)
                {
                    repairPct = (repairCount / totalCount) * 100.0;
                    setupPct = (setupCount / totalCount) * 100.0;
                }
            }

            var ratioText = $"Рем: {repairPct:0.0}%  Настр: {setupPct:0.0}%";
            double summaryFont = 12;
            double approxWidthSummary = Math.Max(40, ratioText.Length * summaryFont * 0.55);
            var ftSummary = new FormattedText(ratioText, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), summaryFont, Brushes.Black);
            // draw pill background
            var pillRect = new Rect(left + plotW - approxWidthSummary - 12, top - annSpace + 4, approxWidthSummary + 12, 20);
            // draw plain repair/setup summary text (no background)
            context.DrawText(ftSummary, new Point(left + plotW - approxWidthSummary - 6, top - annSpace + 6));
        }
    }
}
