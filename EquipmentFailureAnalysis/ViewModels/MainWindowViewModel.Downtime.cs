using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.Utility;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using System.Reactive;
using System.Globalization;

namespace EquipmentFailureAnalysis.ViewModels
{
    public partial class MainWindowViewModel
    {
        private System.Collections.Generic.IEnumerable<Issue> GetFilteredIssues(EquipmentInfo? equipment)
        {
            if (equipment == null)
                return System.Linq.Enumerable.Empty<Issue>();

            var query = (DowntimeEquipmentSearchQuery ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(query))
            {
                var matchesEquipment = (equipment.Title?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false)
                    || (equipment.InventoryNumber?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false);
                if (!matchesEquipment)
                    return System.Linq.Enumerable.Empty<Issue>();
            }

            var source = equipment.Issues.Where(i =>
                (ShowRepairs && i.Type == IssueType.Ремонт) ||
                (ShowSetups && i.Type == IssueType.Настройка));

            source = SelectedDowntimeIssueTypeFilter switch
            {
                "Ремонты" => source.Where(i => i.Type == IssueType.Ремонт),
                "Настройки" => source.Where(i => i.Type == IssueType.Настройка),
                _ => source
            };

            if (!string.Equals(SelectedDowntimeResponsibleFilter, "Все ответственные", StringComparison.CurrentCultureIgnoreCase))
            {
                if (string.Equals(SelectedDowntimeResponsibleFilter, "Без ответственного", StringComparison.CurrentCultureIgnoreCase))
                {
                    source = source.Where(i => IsUnassignedResponsible(i.Responsible));
                }
                else
                {
                    source = source.Where(i => string.Equals(i.Responsible?.Trim(), SelectedDowntimeResponsibleFilter, StringComparison.CurrentCultureIgnoreCase));
                }
            }

            return source;
        }

        // Apply type filters and sort master list into _allEquipment used for UI
        private void ApplyTypeFilterAndSort()
        {
            // Ensure the left column remains ordered by total issue count (descending)
            _allEquipment = _masterEquipment.OrderByDescending(e => e.Issues?.Count ?? 0).ToList();
            EquipmentCollection?.Clear();
            ApplyFilter();
        }

        // Commands to sort left column explicitly
        public ReactiveCommand<Unit, Unit> SortIssuesAscCommand { get; private set; }
        public ReactiveCommand<Unit, Unit> SortIssuesDescCommand { get; private set; }

        // Build timeline and annotations for a given date and equipment using current type filters.
        private void BuildTimelineForDate(DateTime date, EquipmentInfo? equipment)
        {
            if (equipment == null)
                return;
            // compute everything first, then update UI-bound collections on UI thread
            AnalysisDate = date.Date;
            var selIssuesForDate = GetFilteredIssues(equipment).ToList();

            int faultsToday = 0;
            double totalDownMinutes = 0.0;
            var dateStart = date.Date;
            var dateEnd = dateStart.AddDays(1);
            var intervals = new System.Collections.Generic.List<(int sMin, int eMin)>();
            var repairsIntervals = new System.Collections.Generic.List<(int sMin, int eMin)>();
            var setupsIntervals = new System.Collections.Generic.List<(int sMin, int eMin)>();
            var annList = new System.Collections.Generic.List<Models.Annotation>();

            foreach (var issue in selIssuesForDate)
            {
                var overlapStart = issue.Start < dateStart ? dateStart : issue.Start;
                var overlapEnd = issue.End > dateEnd ? dateEnd : issue.End;
                if (overlapEnd <= overlapStart)
                    continue;
                faultsToday++;
                totalDownMinutes += (overlapEnd - overlapStart).TotalMinutes;

                int sMin = Math.Clamp((int)Math.Round((overlapStart - dateStart).TotalMinutes, MidpointRounding.AwayFromZero), 0, 24 * 60);
                int eMin = Math.Clamp((int)Math.Round((overlapEnd - dateStart).TotalMinutes, MidpointRounding.AwayFromZero), 0, 24 * 60);
                if (eMin <= sMin)
                    eMin = Math.Min(24 * 60, sMin + 1);
                intervals.Add((sMin, eMin));
                if (issue.Type == IssueType.Ремонт)
                    repairsIntervals.Add((sMin, eMin));
                else if (issue.Type == IssueType.Настройка)
                    setupsIntervals.Add((sMin, eMin));
                var duration = TimeSpan.FromMinutes(Math.Max(0, eMin - sMin));
                var desc = issue.Description ?? string.Empty;
                var resp = string.IsNullOrEmpty(issue.Responsible) ? "-" : issue.Responsible;
                annList.Add(new Models.Annotation
                {
                    Hour = sMin / 60.0,
                    StartHour = sMin / 60.0,
                    EndHour = eMin / 60.0,
                    Description = desc,
                    Responsible = resp,
                    StartDate = overlapStart,
                    EndDate = overlapEnd,
                    Duration = duration.ToString(@"hh\:mm"),
                    Type = issue.Type,
                    IsInProgress = issue.IsInProgress
                });
            }

            // compute stats
            double downPercent = Math.Min(100.0, (totalDownMinutes / (24.0 * 60.0)) * 100.0);
            double workPercent = 100.0 - downPercent;
            string avgRepair = "0 мин";
            if (faultsToday > 0)
            {
                double avg = totalDownMinutes / faultsToday;
                avgRepair = Math.Round(avg) + " мин";
            }

            var merged = MergeIntervals(intervals);
            var repairsMerged = MergeIntervals(repairsIntervals);
            var setupsMerged = MergeIntervals(setupsIntervals);

            var timelinePoints = BuildTimelinePoints(merged);
            var repairsTimelinePoints = BuildTimelinePoints(repairsMerged);
            var setupsTimelinePoints = BuildTimelinePoints(setupsMerged);

            // Now update UI-bound collections on UI thread
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                FaultsForDay = faultsToday;
                WorkPercent = workPercent;
                DowntimePercent = downPercent.ToString("0.0") + "%";
                DownPercent = downPercent.ToString("0.0") + "%";
                AvgRepairTime = avgRepair;

                DayTimeline.Clear();
                for (int h = 0; h < 24; h++)
                    DayTimeline.Add(0);
                foreach (var m in merged)
                {
                    int startHour = (int)Math.Floor(m.sMin / 60.0);
                    int endHour = (int)Math.Ceiling(m.eMin / 60.0);
                    startHour = Math.Clamp(startHour, 0, 23);
                    endHour = Math.Clamp(endHour, 0, 24);
                    for (int h = startHour; h < endHour; h++)
                        DayTimeline[h] = 1;
                }

                // replace collections so bindings update and TimelineControl gets notified
                DayTimelinePoints = new ObservableCollection<Models.TimelinePoint>(timelinePoints);
                RepairsTimelinePoints = new ObservableCollection<Models.TimelinePoint>(repairsTimelinePoints);
                SetupsTimelinePoints = new ObservableCollection<Models.TimelinePoint>(setupsTimelinePoints);
                Annotations = new ObservableCollection<Models.Annotation>(
                    annList.OrderByDescending(a => TimeSpan.TryParse(a.Duration, out var parsed) ? parsed : TimeSpan.Zero));
                // update counts for the selected day (issues overlapping that date)
                try
                {
                    var selStart = date.Date;
                    var selEnd = selStart.AddDays(1);
                    SelectedDayRepairs = equipment.Issues.Count(i => i.Type == IssueType.Ремонт && i.End > selStart && i.Start < selEnd);
                    SelectedDaySetups = equipment.Issues.Count(i => i.Type == IssueType.Настройка && i.End > selStart && i.Start < selEnd);
                }
                catch { }
            });
        }

        private void BuildDowntimeHeatmap()
        {
            DowntimeMonthRows.Clear();
            var year = DateTime.Now.Year;
            var filteredEquipment = FilterDowntimeEquipmentByQuery(_masterEquipment);

            for (int month = 1; month <= 12; month++)
            {
                var monthDate = new DateTime(year, month, 1);
                var daysInMonth = DateTime.DaysInMonth(monthDate.Year, monthDate.Month);
                var monthRow = new Models.MonthRow
                {
                    Month = monthDate.Month,
                    Year = monthDate.Year,
                    MonthName = monthDate.ToString("MMM")
                };

                for (int d = 1; d <= 31; d++)
                {
                    var isValid = d <= daysInMonth;
                    var cell = new Models.DayCell { DayNumber = d, Index = 0, IsValid = isValid };

                    if (isValid)
                    {
                        var day = new DateTime(monthDate.Year, monthDate.Month, d);
                        var dayEnd = day.AddDays(1);
                        cell.Date = day;
                        cell.Index = filteredEquipment.Count(eq => GetDowntimeFilteredIssues(eq, day, dayEnd).Any());
                    }

                    monthRow.Days.Add(cell);
                }

                DowntimeMonthRows.Add(monthRow);
            }
        }

        private void BuildDowntimeDayEquipmentRows(DateTime date)
        {
            DowntimeAnalysisDate = date.Date;
            DowntimeDayEquipmentRows.Clear();

            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);

            var rows = new System.Collections.Generic.List<Models.DowntimeEquipmentRow>();
            int totalIssues = 0;
            int totalRepairs = 0;
            int totalSetups = 0;
            double totalMergedDownMinutes = 0.0;
            var affectedByHour = new int[24];

            foreach (var equipment in FilterDowntimeEquipmentByQuery(_masterEquipment))
            {
                var issuesForDay = GetDowntimeFilteredIssues(equipment, dayStart, dayEnd).ToList();
                if (issuesForDay.Count == 0)
                    continue;

                totalIssues += issuesForDay.Count;
                totalRepairs += issuesForDay.Count(i => i.Type == IssueType.Ремонт);
                totalSetups += issuesForDay.Count(i => i.Type == IssueType.Настройка);

                var intervals = new System.Collections.Generic.List<(int sMin, int eMin)>();
                var repairsIntervals = new System.Collections.Generic.List<(int sMin, int eMin)>();
                var setupsIntervals = new System.Collections.Generic.List<(int sMin, int eMin)>();

                var rowAnnotations = new System.Collections.Generic.List<Models.Annotation>();

                foreach (var issue in issuesForDay)
                {
                    var overlapStart = issue.Start < dayStart ? dayStart : issue.Start;
                    var overlapEnd = issue.End > dayEnd ? dayEnd : issue.End;
                    if (overlapEnd <= overlapStart)
                        continue;

                    int sMin = Math.Clamp((int)Math.Round((overlapStart - dayStart).TotalMinutes, MidpointRounding.AwayFromZero), 0, 24 * 60);
                    int eMin = Math.Clamp((int)Math.Round((overlapEnd - dayStart).TotalMinutes, MidpointRounding.AwayFromZero), 0, 24 * 60);
                    if (eMin <= sMin)
                        eMin = Math.Min(24 * 60, sMin + 1);

                    intervals.Add((sMin, eMin));
                    if (issue.Type == IssueType.Ремонт)
                        repairsIntervals.Add((sMin, eMin));
                    else if (issue.Type == IssueType.Настройка)
                        setupsIntervals.Add((sMin, eMin));

                    rowAnnotations.Add(new Models.Annotation
                    {
                        Hour = sMin / 60.0,
                        StartHour = sMin / 60.0,
                        EndHour = eMin / 60.0,
                        Description = issue.Description ?? string.Empty,
                        Responsible = string.IsNullOrWhiteSpace(issue.Responsible) ? "-" : issue.Responsible,
                        StartDate = overlapStart,
                        EndDate = overlapEnd,
                        Duration = TimeSpan.FromMinutes(Math.Max(0, eMin - sMin)).ToString(@"hh\:mm"),
                        Type = issue.Type,
                        IsInProgress = issue.IsInProgress
                    });
                }

                var merged = MergeIntervals(intervals);
                var repairsMerged = MergeIntervals(repairsIntervals);
                var setupsMerged = MergeIntervals(setupsIntervals);

                totalMergedDownMinutes += merged.Sum(m => Math.Max(0, m.eMin - m.sMin));
                foreach (var m in merged)
                {
                    int startHour = Math.Clamp((int)Math.Floor(m.sMin / 60.0), 0, 23);
                    int endHour = Math.Clamp((int)Math.Ceiling(m.eMin / 60.0), 0, 24);
                    for (int h = startHour; h < endHour; h++)
                        affectedByHour[h]++;
                }

                rows.Add(new Models.DowntimeEquipmentRow
                {
                    Equipment = equipment,
                    Title = equipment.Title,
                    InventoryNumber = equipment.InventoryNumber ?? "-",
                    IssuesCount = issuesForDay.Count,
                    TimelinePoints = new ObservableCollection<Models.TimelinePoint>(BuildTimelinePoints(merged)),
                    RepairsTimelinePoints = new ObservableCollection<Models.TimelinePoint>(BuildTimelinePoints(repairsMerged)),
                    SetupsTimelinePoints = new ObservableCollection<Models.TimelinePoint>(BuildTimelinePoints(setupsMerged)),
                    Annotations = new ObservableCollection<Models.Annotation>(
                        rowAnnotations.OrderBy(a => a.StartHour).ThenBy(a => a.EndHour))
                });
            }

            foreach (var row in rows.OrderByDescending(r => r.IssuesCount))
                DowntimeDayEquipmentRows.Add(row);

            DowntimeAffectedEquipmentCount = rows.Count;
            DowntimeTotalIssues = totalIssues;
            DowntimeRepairsCount = totalRepairs;
            DowntimeSetupsCount = totalSetups;
            DowntimeAffectedSharePercent = _masterEquipment.Count == 0 ? 0.0 : rows.Count * 100.0 / _masterEquipment.Count;
            DowntimeTotalDuration = TimeSpan.FromMinutes(totalMergedDownMinutes).ToString(@"hh\:mm");
            DowntimeAvgIssuesPerEquipment = rows.Count == 0 ? "0.0" : (totalIssues / (double)rows.Count).ToString("0.0");

            int peakCount = affectedByHour.Max();
            if (peakCount > 0)
            {
                int peakHour = Array.IndexOf(affectedByHour, peakCount);
                DowntimePeakHour = $"{peakHour:00}:00 ({peakCount})";
            }
            else
            {
                DowntimePeakHour = "-";
            }

            var top = rows.OrderByDescending(r => r.IssuesCount).ThenBy(r => r.Title).FirstOrDefault();
            DowntimeTopEquipment = AddSoftWrapOpportunities(top?.Title ?? "-");
            DowntimeTopEquipmentIssues = top?.IssuesCount ?? 0;
        }

        private void RefreshDowntimeAnalysis()
        {
            if (_masterEquipment.Count == 0)
                return;

            BuildDowntimeHeatmap();
            BuildDowntimeDayEquipmentRows(DowntimeAnalysisDate);
        }

        private void RefreshFailureAnalysis()
        {
            if (_masterEquipment.Count == 0 || SelectedEquipment == null)
                return;

            if (LoadEquipmentCommand == null)
                return;

            var selectedDate = AnalysisDate.Date;

            LoadEquipmentCommand.Execute(SelectedEquipment).Subscribe(_ =>
            {
                if (ShowDayTimelineCommand != null)
                    ShowDayTimelineCommand.Execute(selectedDate).Subscribe();
            });
        }

        private void ApplyHeatmapColorRange()
        {
            ValueToColorConverter.SetHeatmapRange(ValueToColorConverter.FailureHeatmapKey, _failureHeatmapColorMin, _failureHeatmapColorMax);
            ValueToColorConverter.SetHeatmapRange(ValueToColorConverter.DowntimeHeatmapKey, _downtimeHeatmapColorMin, _downtimeHeatmapColorMax);

            RefreshDowntimeAnalysis();
            RefreshFailureAnalysis();
        }

        private void ResetUniversalFilters()
        {
            SelectedDowntimeIssueTypeFilter = "Все типы";
            SelectedDowntimeResponsibleFilter = "Все ответственные";
            SelectedDowntimeSubdivisionFilter = "Все группы";
            DowntimeEquipmentSearchQuery = string.Empty;
        }

        private void RebuildDowntimeResponsibleFilters()
        {
            var previous = SelectedDowntimeResponsibleFilter;
            DowntimeResponsibleFilters.Clear();
            DowntimeResponsibleFilters.Add("Все ответственные");
            DowntimeResponsibleFilters.Add("Без ответственного");

            foreach (var responsible in _masterEquipment
                .SelectMany(eq => eq.Issues)
                .Select(i => i.Responsible?.Trim())
                .Where(r => !string.IsNullOrWhiteSpace(r) && !IsUnassignedResponsible(r))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(r => r, StringComparer.CurrentCultureIgnoreCase))
            {
                DowntimeResponsibleFilters.Add(responsible!);
            }

            SelectedDowntimeResponsibleFilter = DowntimeResponsibleFilters.Contains(previous)
                ? previous
                : "Все ответственные";
        }

        private void RebuildDowntimeSubdivisionFilters()
        {
            var previous = SelectedDowntimeSubdivisionFilter;
            DowntimeSubdivisionFilters.Clear();
            DowntimeSubdivisionFilters.Add("Все группы");
            DowntimeSubdivisionFilters.Add("Без группы");

            foreach (var subdivision in _masterEquipment
                .Select(eq => eq.Subdivision?.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase))
            {
                DowntimeSubdivisionFilters.Add(subdivision!);
            }

            SelectedDowntimeSubdivisionFilter = DowntimeSubdivisionFilters.Contains(previous)
                ? previous
                : "Все группы";
        }

        private bool MatchesDowntimeSubdivision(EquipmentInfo equipment)
        {
            if (string.Equals(SelectedDowntimeSubdivisionFilter, "Все группы", StringComparison.CurrentCultureIgnoreCase))
                return true;

            if (string.Equals(SelectedDowntimeSubdivisionFilter, "Без группы", StringComparison.CurrentCultureIgnoreCase))
                return string.IsNullOrWhiteSpace(equipment.Subdivision);

            return string.Equals(equipment.Subdivision?.Trim(), SelectedDowntimeSubdivisionFilter, StringComparison.CurrentCultureIgnoreCase);
        }

        private System.Collections.Generic.List<EquipmentInfo> FilterDowntimeEquipmentByQuery(System.Collections.Generic.IEnumerable<EquipmentInfo> source)
        {
            var filteredBySubdivision = source.Where(MatchesDowntimeSubdivision);
            var query = (DowntimeEquipmentSearchQuery ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
                return filteredBySubdivision.ToList();

            return filteredBySubdivision
                .Where(eq => (eq.Title?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false)
                    || (eq.InventoryNumber?.Contains(query, StringComparison.CurrentCultureIgnoreCase) ?? false))
                .ToList();
        }

        private System.Collections.Generic.IEnumerable<Issue> GetDowntimeFilteredIssues(EquipmentInfo equipment, DateTime start, DateTime end)
        {
            if (!MatchesDowntimeSubdivision(equipment))
                return System.Linq.Enumerable.Empty<Issue>();

            var source = equipment.Issues.Where(issue => issue.End > start && issue.Start < end);

            source = SelectedDowntimeIssueTypeFilter switch
            {
                "Ремонты" => source.Where(i => i.Type == IssueType.Ремонт),
                "Настройки" => source.Where(i => i.Type == IssueType.Настройка),
                _ => source
            };

            if (!string.Equals(SelectedDowntimeResponsibleFilter, "Все ответственные", StringComparison.CurrentCultureIgnoreCase))
            {
                if (string.Equals(SelectedDowntimeResponsibleFilter, "Без ответственного", StringComparison.CurrentCultureIgnoreCase))
                {
                    source = source.Where(i => IsUnassignedResponsible(i.Responsible));
                }
                else
                {
                    source = source.Where(i => string.Equals(i.Responsible?.Trim(), SelectedDowntimeResponsibleFilter, StringComparison.CurrentCultureIgnoreCase));
                }
            }

            return source;
        }

        private static string AddSoftWrapOpportunities(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "-";

            var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length <= 16)
                    continue;

                var token = parts[i];
                var chunked = new System.Text.StringBuilder(token.Length + (token.Length / 16));
                for (int j = 0; j < token.Length; j++)
                {
                    chunked.Append(token[j]);
                    if ((j + 1) % 16 == 0 && j < token.Length - 1)
                        chunked.Append('\u200B');
                }

                parts[i] = chunked.ToString();
            }

            return string.Join(" ", parts);
        }

        private static System.Collections.Generic.List<(int sMin, int eMin)> MergeIntervals(System.Collections.Generic.List<(int sMin, int eMin)> intervals)
        {
            var merged = new System.Collections.Generic.List<(int sMin, int eMin)>();
            if (intervals.Count == 0)
                return merged;

            intervals.Sort((a, b) => a.sMin.CompareTo(b.sMin));
            var cur = intervals[0];
            for (int i = 1; i < intervals.Count; i++)
            {
                var it = intervals[i];
                if (it.sMin <= cur.eMin + 1)
                {
                    cur.eMin = Math.Max(cur.eMin, it.eMin);
                }
                else
                {
                    merged.Add(cur);
                    cur = it;
                }
            }
            merged.Add(cur);
            return merged;
        }

        private static System.Collections.Generic.List<Models.TimelinePoint> BuildTimelinePoints(System.Collections.Generic.List<(int sMin, int eMin)> merged)
        {
            var points = new System.Collections.Generic.List<Models.TimelinePoint>();

            if (merged.Count == 0)
            {
                points.Add(new Models.TimelinePoint { Hour = 0.0, Value = 0 });
                points.Add(new Models.TimelinePoint { Hour = 24.0, Value = 0 });
                return points;
            }

            int startValue = merged[0].sMin <= 0 ? 1 : 0;
            points.Add(new Models.TimelinePoint { Hour = 0.0, Value = startValue });

            foreach (var m in merged)
            {
                if (m.sMin > 0)
                    points.Add(new Models.TimelinePoint { Hour = m.sMin / 60.0, Value = 1 });

                if (m.eMin < 24 * 60)
                    points.Add(new Models.TimelinePoint { Hour = m.eMin / 60.0, Value = 0 });
            }

            int endValue = merged[merged.Count - 1].eMin >= 24 * 60 ? 1 : 0;
            points.Add(new Models.TimelinePoint { Hour = 24.0, Value = endValue });

            return points;
        }
    }
}
