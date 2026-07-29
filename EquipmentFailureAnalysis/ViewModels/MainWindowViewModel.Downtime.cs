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

            source = SelectedDowntimeStatusFilter switch
            {
                "В процессе" => source.Where(i => i.IsInProgress),
                "Завершена" => source.Where(i => !i.IsInProgress),
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
            {
                SelectedTimelineAnnotation = null;
                return;
            }
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
                var actualDuration = issue.End - issue.Start;
                if (actualDuration < TimeSpan.Zero)
                    actualDuration = TimeSpan.Zero;
                var actHours = (int)actualDuration.TotalHours;
                var formattedDuration = $"{actHours:00}:{actualDuration.Minutes:00}";

                var desc = issue.Description ?? string.Empty;
                var resp = string.IsNullOrEmpty(issue.Responsible) ? "-" : issue.Responsible;
                annList.Add(new Models.Annotation
                {
                    Hour = sMin / 60.0,
                    StartHour = sMin / 60.0,
                    EndHour = eMin / 60.0,
                    Description = desc,
                    Responsible = resp,
                    StartDate = issue.Start,
                    EndDate = issue.End,
                    Duration = formattedDuration,
                    Type = issue.Type,
                    JiraIssueKey = issue.JiraIssueKey ?? string.Empty,
                    Reporter = issue.Reporter ?? string.Empty,
                    Comments = issue.Comments ?? string.Empty,
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
                var sortedAnnotations = annList
                    .OrderByDescending(a => TimeSpan.TryParse(a.Duration, out var parsed) ? parsed : TimeSpan.Zero)
                    .ToList();
                Annotations = new ObservableCollection<Models.Annotation>(sortedAnnotations);
                SelectedTimelineAnnotation = sortedAnnotations.FirstOrDefault();
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
            Downtime.BuildHeatmap(_masterEquipment);
        }

        private void BuildDowntimeDayEquipmentRows(DateTime date)
        {
            Downtime.BuildDayEquipmentRows(_masterEquipment, date);
        }

        private void RefreshDowntimeAnalysis()
        {
            if (_masterEquipment.Count == 0)
                return;

            Downtime.Refresh(_masterEquipment, DowntimeAnalysisDate);
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

        internal void HandleDowntimeFilterChanged()
        {
            RefreshFailureAnalysis();
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
            SelectedDowntimeStatusFilter = "Все статусы";
            SelectedDowntimeResponsibleFilter = "Все ответственные";
            SelectedDowntimeSubdivisionFilter = "Все группы";
            DowntimeEquipmentSearchQuery = string.Empty;
        }

        private void RebuildDowntimeResponsibleFilters()
        {
            Downtime.RebuildResponsibleFilters(_masterEquipment);
        }

        private void RebuildDowntimeSubdivisionFilters()
        {
            Downtime.RebuildSubdivisionFilters(_masterEquipment);
        }

        private bool MatchesDowntimeSubdivision(EquipmentInfo equipment)
        {
            if (string.Equals(SelectedDowntimeSubdivisionFilter, "Все группы", StringComparison.CurrentCultureIgnoreCase))
                return true;

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

            source = SelectedDowntimeStatusFilter switch
            {
                "В процессе" => source.Where(i => i.IsInProgress),
                "Завершена" => source.Where(i => !i.IsInProgress),
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
