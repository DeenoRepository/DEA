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
        // Refresh daily indices and month rows for UI for a given equipment without reassigning commands
        private void RefreshEquipmentView(EquipmentInfo equipment)
        {
            if (equipment == null) return;

            DailyDowntimeIndexCollection.Clear();
            for (int i = 0; i < 365; i++)
            {
                DailyDowntimeIndexCollection.Add(new DailyDowntimeIndex { Day = DateTime.Now.AddDays(-i), Index = 0 });
            }

            // mark selection
            foreach (var eq in EquipmentCollection)
                eq.IsSelected = false;
            SelectedEquipment = equipment;
            if (SelectedEquipment != null)
                SelectedEquipment.IsSelected = true;

            var filteredIssues = GetFilteredIssues(equipment).ToList();

            // compute daily indices
            foreach (var issue in filteredIssues)
            {
                var startDate = issue.Start.Date;
                var endDate = issue.End.Date;
                if (endDate < startDate)
                    endDate = startDate;

                for (var day = startDate; day <= endDate; day = day.AddDays(1))
                {
                    var daysAgo = (DateTime.Now.Date - day).Days;
                    if (daysAgo >= 0 && daysAgo < DailyDowntimeIndexCollection.Count)
                    {
                        DailyDowntimeIndexCollection[daysAgo].Index++;
                    }
                }
            }

            // build month rows for calendar year (January..December)
            MonthRows.Clear();
            var year = DateTime.Now.Year;
            for (int month = 1; month <= 12; month++)
            {
                var monthDate = new DateTime(year, month, 1);
                var daysInMonth = DateTime.DaysInMonth(monthDate.Year, monthDate.Month);
                var monthRow = new Models.MonthRow { Month = monthDate.Month, Year = monthDate.Year, MonthName = monthDate.ToString("MMM") };

                for (int d = 1; d <= 31; d++)
                {
                    var isValid = d <= daysInMonth;
                    var cell = new Models.DayCell { DayNumber = d, Index = 0, IsValid = isValid };
                    if (isValid)
                    {
                        cell.Date = new DateTime(monthDate.Year, monthDate.Month, d);
                        var entry = DailyDowntimeIndexCollection.FirstOrDefault(x => x.Day.Date == cell.Date.Date);
                        if (entry != null) cell.Index = entry.Index;
                    }
                    monthRow.Days.Add(cell);
                }
                MonthRows.Add(monthRow);
            }
        }

        // Called from view when user imports a new XML file. Sorts equipment by issue count and refreshes view.
        public void ImportEquipment(ObservableCollection<EquipmentInfo> imported)
        {
            if (imported == null)
                return;

            var all = imported.ToList();
            _masterEquipment = all;
            RebuildDowntimeResponsibleFilters();
            RebuildDowntimeSubdivisionFilters();
            RebuildEmployeeMonthOptions();
            RebuildEmployeeSubdivisionFilters();
            // keep left column ordering by total issues
            _allEquipment = _masterEquipment.OrderByDescending(e => e.Issues?.Count ?? 0).ToList();
            EquipmentCollection.Clear();
            ApplyFilter();
            FillSearchWithFirstEquipmentIfNeeded(force: true);

            // auto-select first
            if (EquipmentCollection.Count > 0)
            {
                var first = EquipmentCollection[0];
                LoadEquipmentCommand.Execute(first).Subscribe(_ =>
                {
                    if (ShowDayTimelineCommand != null)
                        ShowDayTimelineCommand.Execute(DateTime.Now.Date).Subscribe();
                });
            }

            BuildDowntimeHeatmap();
            BuildDowntimeDayEquipmentRows(DateTime.Now.Date);
            BuildEmployeeAnalysis();
        }

        private void BuildEmployeeAnalysis()
        {
            var issuesWithEquipmentAll = _masterEquipment
                .SelectMany(eq => eq.Issues.Select(issue => new EmployeeIssueProjection { Equipment = eq, Issue = issue }))
                .ToList();

            var issuesWithEquipment = FilterEmployeeIssuesBySelectedSubdivision(
                FilterEmployeeIssuesBySelectedMonth(issuesWithEquipmentAll)).ToList();

            EmployeeTotalIssues = issuesWithEquipment.Count;

            var assignedIssues = issuesWithEquipment
                .Where(x => !IsUnassignedResponsible(x.Issue.Responsible))
                .ToList();

            EmployeeUnassignedIssues = issuesWithEquipment.Count - assignedIssues.Count;
            EmployeeRepairsTotal = assignedIssues.Count(x => x.Issue.Type == IssueType.Ремонт);
            EmployeeSetupsTotal = assignedIssues.Count(x => x.Issue.Type == IssueType.Настройка);

            var slaMetTotal = assignedIssues.Count(x => Math.Max(0, (x.Issue.End - x.Issue.Start).TotalMinutes) <= SlaTargetMinutes);
            EmployeeSlaBreaches = Math.Max(0, assignedIssues.Count - slaMetTotal);
            EmployeeSlaCompliancePercent = assignedIssues.Count == 0
                ? 0.0
                : slaMetTotal * 100.0 / assignedIssues.Count;

            EmployeeCoveragePercent = EmployeeTotalIssues == 0
                ? 0.0
                : assignedIssues.Count * 100.0 / EmployeeTotalIssues;

            var rows = assignedIssues
                .GroupBy(x => x.Issue.Responsible!.Trim(), StringComparer.CurrentCultureIgnoreCase)
                .Select(g =>
                {
                    var issues = g.Select(x => x.Issue).ToList();
                    var totalDuration = TimeSpan.FromMinutes(issues.Sum(i => Math.Max(0, (i.End - i.Start).TotalMinutes)));
                    var avgMinutes = issues.Count == 0 ? 0.0 : totalDuration.TotalMinutes / issues.Count;
                    var lastIssueDate = issues.Count == 0 ? DateTime.MinValue : issues.Max(i => i.End);

                    return new Models.EmployeeAnalysisRow
                    {
                        Name = g.Key,
                        Subdivision = string.Join(", ", g
                            .Select(x => x.Equipment.Subdivision)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => s!.Trim())
                            .Distinct(StringComparer.CurrentCultureIgnoreCase)),
                        IssuesCount = issues.Count,
                        EventSharePercent = assignedIssues.Count == 0 ? 0.0 : issues.Count * 100.0 / assignedIssues.Count,
                        RepairsCount = issues.Count(i => i.Type == IssueType.Ремонт),
                        SetupsCount = issues.Count(i => i.Type == IssueType.Настройка),
                        RepairsSharePercent = issues.Count == 0 ? 0.0 : issues.Count(i => i.Type == IssueType.Ремонт) * 100.0 / issues.Count,
                        SlaMetCount = issues.Count(i => Math.Max(0, (i.End - i.Start).TotalMinutes) <= SlaTargetMinutes),
                        SlaCompliancePercent = issues.Count == 0
                            ? 0.0
                            : issues.Count(i => Math.Max(0, (i.End - i.Start).TotalMinutes) <= SlaTargetMinutes) * 100.0 / issues.Count,
                        EquipmentCount = g.Select(x => x.Equipment.Title).Distinct(StringComparer.CurrentCultureIgnoreCase).Count(),
                        TotalDuration = totalDuration,
                        TotalDurationText = FormatDuration(totalDuration),
                        AvgDurationMinutes = avgMinutes,
                        AvgDurationText = FormatDuration(TimeSpan.FromMinutes(avgMinutes)),
                        LastIssueDate = lastIssueDate,
                        LastIssueDateText = lastIssueDate == DateTime.MinValue ? "-" : lastIssueDate.ToString("dd.MM.yyyy")
                    };
                })
                .Select(r =>
                {
                    if (string.IsNullOrWhiteSpace(r.Subdivision))
                        r.Subdivision = "-";
                    return r;
                })
                .ToList();

            if (rows.Count > 0)
            {
                const double repairComplexityWeight = 1.35;
                const double setupComplexityWeight = 1.0;

                var maxIssues = Math.Max(1, rows.Max(r => r.IssuesCount));
                var maxEquipment = Math.Max(1, rows.Max(r => r.EquipmentCount));
                var minAvgMinutes = rows.Min(r => r.AvgDurationMinutes);
                var maxAvgMinutes = rows.Max(r => r.AvgDurationMinutes);
                var avgSpan = Math.Max(1.0, maxAvgMinutes - minAvgMinutes);
                var maxComplexityLoad = Math.Max(
                    1.0,
                    rows.Max(r => r.RepairsCount * repairComplexityWeight + r.SetupsCount * setupComplexityWeight));

                foreach (var row in rows)
                {
                    var shareScore = Math.Clamp(row.EventSharePercent, 0, 100);
                    var slaScore = Math.Clamp(row.SlaCompliancePercent, 0, 100);
                    var speedScore = Math.Clamp(100.0 - ((row.AvgDurationMinutes - minAvgMinutes) / avgSpan) * 100.0, 0, 100);
                    var loadScore = row.IssuesCount * 100.0 / maxIssues;
                    var complexityLoadScore = (row.RepairsCount * repairComplexityWeight + row.SetupsCount * setupComplexityWeight)
                        * 100.0 / maxComplexityLoad;
                    var coverageScore = row.EquipmentCount * 100.0 / maxEquipment;

                    var score = slaScore * 0.40
                                + shareScore * 0.15
                                + speedScore * 0.15
                                + loadScore * 0.10
                                + complexityLoadScore * 0.10
                                + coverageScore * 0.10;

                    row.PerformanceScore = Math.Round(score, 1);
                    var grade = row.PerformanceScore >= 85 ? "A"
                        : row.PerformanceScore >= 70 ? "B"
                        : row.PerformanceScore >= 55 ? "C"
                        : "D";
                    row.PerformanceSummary = $"{row.PerformanceScore:0.0} ({grade})";
                }
            }

            rows = rows
                .OrderByDescending(r => r.PerformanceScore)
                .ThenByDescending(r => r.SlaCompliancePercent)
                .ThenByDescending(r => r.IssuesCount)
                .ThenBy(r => r.Name)
                .ToList();

            RebuildEmployeeTimelineEmployees(rows.Select(r => r.Name));

            EmployeeAnalysisRows = new ObservableCollection<Models.EmployeeAnalysisRow>(rows);
            EmployeeTotalCount = rows.Count;
            EmployeeAvgEquipmentPerEmployee = rows.Count == 0 ? "0.0" : rows.Average(r => r.EquipmentCount).ToString("0.0");

            var avgDurationMinutes = assignedIssues.Count == 0
                ? 0.0
                : assignedIssues.Sum(x => Math.Max(0, (x.Issue.End - x.Issue.Start).TotalMinutes)) / assignedIssues.Count;
            EmployeeAvgDuration = FormatDuration(TimeSpan.FromMinutes(avgDurationMinutes));

            var topByIssues = rows.OrderByDescending(r => r.IssuesCount).ThenBy(r => r.Name).FirstOrDefault();
            EmployeeTopByIssues = topByIssues?.Name ?? "-";
            EmployeeTopByIssuesValue = topByIssues == null ? "0" : $"{topByIssues.IssuesCount} событий";

            var topByDuration = rows.OrderByDescending(r => r.TotalDuration).ThenBy(r => r.Name).FirstOrDefault();
            EmployeeTopByDuration = topByDuration?.Name ?? "-";
            EmployeeTopByDurationValue = topByDuration?.TotalDurationText ?? "00:00";

            EmployeeAnalysisPeriodDescription = string.IsNullOrWhiteSpace(SelectedEmployeeAnalysisMonth)
                ? "Текущий месяц"
                : SelectedEmployeeAnalysisMonth;

            BuildEmployeeSelectedDayTimeline();

            BuildDashboard();
        }

        private void RebuildEmployeeTimelineEmployees(System.Collections.Generic.IEnumerable<string> employeeNames)
        {
            var previous = SelectedEmployeeTimelineEmployee;
            EmployeeTimelineEmployees.Clear();
            EmployeeTimelineEmployees.Add("Все сотрудники");

            foreach (var name in employeeNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase))
            {
                EmployeeTimelineEmployees.Add(name);
            }

            var selected = !string.IsNullOrWhiteSpace(previous) && EmployeeTimelineEmployees.Contains(previous)
                ? previous
                : "Все сотрудники";

            if (string.Equals(_selectedEmployeeTimelineEmployee, selected, StringComparison.CurrentCulture))
                this.RaiseAndSetIfChanged(ref _selectedEmployeeTimelineEmployee, string.Empty);

            SelectedEmployeeTimelineEmployee = selected;
        }

        private void BuildEmployeeSelectedDayTimeline()
        {
            var date = (EmployeeTimelineDate?.Date ?? DateTime.Now.Date);
            var dayStart = date;
            var dayEnd = dayStart.AddDays(1);
            var selectedEmployee = (SelectedEmployeeTimelineEmployee ?? "Все сотрудники").Trim();

            var issuesForDay = _masterEquipment
                .SelectMany(eq => eq.Issues)
                .Where(i => i.End > dayStart && i.Start < dayEnd)
                .Where(i => !IsUnassignedResponsible(i.Responsible))
                .Where(i => string.Equals(selectedEmployee, "Все сотрудники", StringComparison.CurrentCultureIgnoreCase)
                    || string.Equals(i.Responsible?.Trim(), selectedEmployee, StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            var intervals = new System.Collections.Generic.List<(int sMin, int eMin)>();
            var repairsIntervals = new System.Collections.Generic.List<(int sMin, int eMin)>();
            var setupsIntervals = new System.Collections.Generic.List<(int sMin, int eMin)>();
            var annotations = new System.Collections.Generic.List<Models.Annotation>();

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

                annotations.Add(new Models.Annotation
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

            EmployeeTimelinePoints = new ObservableCollection<Models.TimelinePoint>(BuildTimelinePoints(MergeIntervals(intervals)));
            EmployeeRepairsTimelinePoints = new ObservableCollection<Models.TimelinePoint>(BuildTimelinePoints(MergeIntervals(repairsIntervals)));
            EmployeeSetupsTimelinePoints = new ObservableCollection<Models.TimelinePoint>(BuildTimelinePoints(MergeIntervals(setupsIntervals)));
            EmployeeTimelineAnnotations = new ObservableCollection<Models.Annotation>(annotations.OrderBy(a => a.StartHour).ThenBy(a => a.EndHour));
        }

        private void BuildDashboard()
        {
            var now = DateTime.Now;
            var currentPeriodStart = now.Date.AddDays(-30);
            var currentPeriodEnd = now.Date.AddDays(1);
            var previousPeriodStart = currentPeriodStart.AddDays(-30);
            var previousPeriodEnd = currentPeriodStart;

            var currentIssues = GetIssuesOverlappingPeriod(currentPeriodStart, currentPeriodEnd).ToList();
            var previousIssues = GetIssuesOverlappingPeriod(previousPeriodStart, previousPeriodEnd).ToList();

            DashboardCurrentPeriodIssues = currentIssues.Count;
            DashboardPreviousPeriodIssues = previousIssues.Count;

            var previousBaseline = Math.Max(1, DashboardPreviousPeriodIssues);
            DashboardIssuesTrendPercent = (DashboardCurrentPeriodIssues - DashboardPreviousPeriodIssues) * 100.0 / previousBaseline;
            if (DashboardCurrentPeriodIssues > DashboardPreviousPeriodIssues)
                DashboardIssuesTrendText = "Рост нагрузки";
            else if (DashboardCurrentPeriodIssues < DashboardPreviousPeriodIssues)
                DashboardIssuesTrendText = "Снижение нагрузки";
            else
                DashboardIssuesTrendText = "Стабильно";

            DashboardCurrentPeriodRepairs = currentIssues.Count(i => i.Issue.Type == IssueType.Ремонт);
            DashboardCurrentPeriodSetups = currentIssues.Count(i => i.Issue.Type == IssueType.Настройка);

            var avgDurationMinutes = currentIssues.Count == 0
                ? 0.0
                : currentIssues.Average(i => Math.Max(0, (i.Issue.End - i.Issue.Start).TotalMinutes));
            DashboardCurrentPeriodAvgDuration = FormatDuration(TimeSpan.FromMinutes(avgDurationMinutes));
            var repairIssues = currentIssues
                .Where(i => i.Issue.Type == IssueType.Ремонт)
                .ToList();
            var mttrMinutes = repairIssues.Count == 0
                ? 0.0
                : repairIssues.Average(i => Math.Max(0, (i.Issue.End - i.Issue.Start).TotalMinutes));
            Dashboard.DashboardCurrentPeriodMttr = FormatDuration(TimeSpan.FromMinutes(mttrMinutes));

            DashboardCurrentPeriodAffectedEquipment = currentIssues
                .Select(i => i.Equipment.Title)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Count();

            var assignedCurrentIssues = currentIssues.Where(i => !IsUnassignedResponsible(i.Issue.Responsible)).ToList();
            var unassignedCurrentIssues = Math.Max(0, currentIssues.Count - assignedCurrentIssues.Count);
            Dashboard.DashboardCurrentPeriodUnassignedSharePercent = currentIssues.Count == 0
                ? 0.0
                : unassignedCurrentIssues * 100.0 / currentIssues.Count;
            DashboardCurrentPeriodActiveEmployees = assignedCurrentIssues
                .Select(i => i.Issue.Responsible!.Trim())
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Count();

            var slaMetCount = assignedCurrentIssues.Count(i => Math.Max(0, (i.Issue.End - i.Issue.Start).TotalMinutes) <= SlaTargetMinutes);
            DashboardCurrentPeriodSlaBreaches = Math.Max(0, assignedCurrentIssues.Count - slaMetCount);
            DashboardCurrentPeriodSlaCompliancePercent = assignedCurrentIssues.Count == 0
                ? 0.0
                : slaMetCount * 100.0 / assignedCurrentIssues.Count;

            var topPerformer = EmployeeAnalysisRows.FirstOrDefault();
            DashboardTopPerformer = topPerformer?.Name ?? "-";
            DashboardTopPerformerValue = topPerformer == null
                ? "Нет данных"
                : $"Оценка {topPerformer.PerformanceSummary}, SLA {topPerformer.SlaCompliancePercent:0.#}%";

            var topRiskEquipment = currentIssues
                .GroupBy(i => i.Equipment.Title, StringComparer.CurrentCultureIgnoreCase)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Name)
                .FirstOrDefault();
            DashboardRiskEquipment = AddSoftWrapOpportunities(topRiskEquipment?.Name ?? "-");
            DashboardRiskEquipmentValue = topRiskEquipment == null ? "0 событий" : $"{topRiskEquipment.Count} событий за 30 дней";

            var recurringByEquipment = currentIssues
                .GroupBy(i => i.Equipment.Title, StringComparer.CurrentCultureIgnoreCase)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .Where(x => x.Count >= 2)
                .ToList();
            var recurringEquipmentCount = recurringByEquipment.Count;
            var recurringEventsCount = recurringByEquipment.Sum(x => x.Count);
            Dashboard.DashboardRecurringFailuresValue = $"{recurringEquipmentCount} ед. / {recurringEventsCount} событий";
            var subdivisionRatings = currentIssues
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Equipment.Subdivision) ? "Без группы" : i.Equipment.Subdivision!.Trim(), StringComparer.CurrentCultureIgnoreCase)
                .Select(g =>
                {
                    var groupIssues = g.ToList();
                    var groupAssigned = groupIssues.Where(x => !IsUnassignedResponsible(x.Issue.Responsible)).ToList();
                    var groupSlaMet = groupAssigned.Count(x => Math.Max(0, (x.Issue.End - x.Issue.Start).TotalMinutes) <= SlaTargetMinutes);
                    var groupSlaPercent = groupAssigned.Count == 0
                        ? 0.0
                        : groupSlaMet * 100.0 / groupAssigned.Count;
                    var groupAvgMinutes = groupIssues.Count == 0
                        ? 0.0
                        : groupIssues.Average(x => Math.Max(0, (x.Issue.End - x.Issue.Start).TotalMinutes));

                    return new Models.SubdivisionRatingRow
                    {
                        Subdivision = g.Key,
                        IssuesCount = groupIssues.Count,
                        ActiveEmployees = groupAssigned
                            .Select(x => x.Issue.Responsible!.Trim())
                            .Distinct(StringComparer.CurrentCultureIgnoreCase)
                            .Count(),
                        SlaCompliancePercent = groupSlaPercent,
                        MttrMinutes = groupAvgMinutes,
                        MttrText = FormatDuration(TimeSpan.FromMinutes(groupAvgMinutes))
                    };
                })
                .ToList();

            if (subdivisionRatings.Count > 0)
            {
                var maxIssuesInSubdivision = Math.Max(1, subdivisionRatings.Max(r => r.IssuesCount));
                var maxMttrInSubdivision = Math.Max(1.0, subdivisionRatings.Max(r => r.MttrMinutes));

                foreach (var row in subdivisionRatings)
                {
                    var incidentScore = 100.0 - (row.IssuesCount * 100.0 / maxIssuesInSubdivision);
                    var mttrScore = 100.0 - (row.MttrMinutes * 100.0 / maxMttrInSubdivision);
                    row.PerformanceScore = Math.Round(
                        row.SlaCompliancePercent * 0.50
                        + incidentScore * 0.30
                        + mttrScore * 0.20,
                        1);
                }
            }

            Dashboard.DashboardSubdivisionRatings = new ObservableCollection<Models.SubdivisionRatingRow>(
                subdivisionRatings
                    .OrderByDescending(r => r.PerformanceScore)
                    .ThenBy(r => r.IssuesCount)
                    .ThenBy(r => r.Subdivision)
                    .Take(5));

            BuildDashboardMonthlyTrends(now);
        }

        private void BuildDashboardMonthlyTrends(DateTime referenceDate)
        {
            var months = new System.Collections.Generic.List<(DateTime start, DateTime end, string label)>();
            var culture = new CultureInfo("ru-RU");

            for (int i = 5; i >= 0; i--)
            {
                var start = new DateTime(referenceDate.Year, referenceDate.Month, 1).AddMonths(-i);
                var end = start.AddMonths(1);
                months.Add((start, end, start.ToString("MMM yyyy", culture)));
            }

            var trendItems = months
                .Select(m =>
                {
                    var monthIssues = GetIssuesOverlappingPeriod(m.start, m.end).ToList();
                    var avgMinutes = monthIssues.Count == 0
                        ? 0.0
                        : monthIssues.Average(x => Math.Max(0, (x.Issue.End - x.Issue.Start).TotalMinutes));

                    var monthAssignedIssues = monthIssues
                        .Where(x => !IsUnassignedResponsible(x.Issue.Responsible))
                        .ToList();
                    var monthSlaMetCount = monthAssignedIssues
                        .Count(x => Math.Max(0, (x.Issue.End - x.Issue.Start).TotalMinutes) <= SlaTargetMinutes);
                    var monthSlaPercent = monthAssignedIssues.Count == 0
                        ? 0.0
                        : monthSlaMetCount * 100.0 / monthAssignedIssues.Count;

                    return new Models.DashboardTrendPoint
                    {
                        PeriodLabel = m.label,
                        IssuesCount = monthIssues.Count,
                        RepairsCount = monthIssues.Count(x => x.Issue.Type == IssueType.Ремонт),
                        SetupsCount = monthIssues.Count(x => x.Issue.Type == IssueType.Настройка),
                        AvgDurationMinutes = avgMinutes,
                        AvgDurationText = FormatDuration(TimeSpan.FromMinutes(avgMinutes)),
                        SlaCompliancePercent = monthSlaPercent
                    };
                })
                .ToList();

            var maxIssues = Math.Max(1, trendItems.Max(t => t.IssuesCount));
            DashboardMaxIssuesInMonth = maxIssues;
            foreach (var item in trendItems)
                item.IntensityPercent = item.IssuesCount * 100.0 / maxIssues;

            DashboardMonthlyTrends = new ObservableCollection<Models.DashboardTrendPoint>(trendItems);
        }

        private System.Collections.Generic.IEnumerable<EmployeeIssueProjection> GetIssuesOverlappingPeriod(DateTime start, DateTime end)
        {
            return _masterEquipment
                .SelectMany(eq => eq.Issues
                    .Where(issue => issue.End > start && issue.Start < end)
                    .Select(issue => new EmployeeIssueProjection { Equipment = eq, Issue = issue }));
        }

        private void RebuildEmployeeMonthOptions()
        {
            var previous = SelectedEmployeeAnalysisMonth;
            var allMonthsOption = "Все месяцы";

            EmployeeAnalysisMonthOptions.Clear();
            EmployeeAnalysisMonthOptions.Add(allMonthsOption);

            var ru = new CultureInfo("ru-RU");
            var months = _masterEquipment
                .SelectMany(eq => eq.Issues)
                .Select(i => new DateTime(i.Start.Year, i.Start.Month, 1))
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            foreach (var month in months)
                EmployeeAnalysisMonthOptions.Add(month.ToString("MMMM yyyy", ru));

            var currentMonth = DateTime.Now.ToString("MMMM yyyy", ru);
            if (!string.IsNullOrWhiteSpace(previous) && EmployeeAnalysisMonthOptions.Contains(previous))
                SelectedEmployeeAnalysisMonth = previous;
            else if (EmployeeAnalysisMonthOptions.Contains(currentMonth))
                SelectedEmployeeAnalysisMonth = currentMonth;
            else
                SelectedEmployeeAnalysisMonth = allMonthsOption;
        }

        private void RebuildEmployeeSubdivisionFilters()
        {
            var previous = SelectedEmployeeSubdivisionFilter;
            EmployeeSubdivisionFilters.Clear();
            EmployeeSubdivisionFilters.Add("Все группы");
            EmployeeSubdivisionFilters.Add("Без группы");

            foreach (var subdivision in _masterEquipment
                .Select(eq => eq.Subdivision?.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase))
            {
                EmployeeSubdivisionFilters.Add(subdivision!);
            }

            if (!string.IsNullOrWhiteSpace(previous) && EmployeeSubdivisionFilters.Contains(previous))
                SelectedEmployeeSubdivisionFilter = previous;
            else
                SelectedEmployeeSubdivisionFilter = "Все группы";
        }

        private System.Collections.Generic.IEnumerable<EmployeeIssueProjection> FilterEmployeeIssuesBySelectedMonth(System.Collections.Generic.IEnumerable<EmployeeIssueProjection> source)
        {
            if (string.IsNullOrWhiteSpace(SelectedEmployeeAnalysisMonth) || SelectedEmployeeAnalysisMonth == "Все месяцы")
                return source;

            var ru = new CultureInfo("ru-RU");
            if (!DateTime.TryParseExact(SelectedEmployeeAnalysisMonth, "MMMM yyyy", ru, DateTimeStyles.None, out var monthDate))
                return source;

            var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            return source.Where(x => x.Issue.Start < monthEnd && x.Issue.End >= monthStart);
        }

        private System.Collections.Generic.IEnumerable<EmployeeIssueProjection> FilterEmployeeIssuesBySelectedSubdivision(System.Collections.Generic.IEnumerable<EmployeeIssueProjection> source)
        {
            if (string.IsNullOrWhiteSpace(SelectedEmployeeSubdivisionFilter)
                || string.Equals(SelectedEmployeeSubdivisionFilter, "Все группы", StringComparison.CurrentCultureIgnoreCase))
                return source;

            if (string.Equals(SelectedEmployeeSubdivisionFilter, "Без группы", StringComparison.CurrentCultureIgnoreCase))
                return source.Where(x => string.IsNullOrWhiteSpace(x.Equipment.Subdivision));

            return source.Where(x => string.Equals(x.Equipment.Subdivision?.Trim(), SelectedEmployeeSubdivisionFilter, StringComparison.CurrentCultureIgnoreCase));
        }

        private static bool IsUnassignedResponsible(string? responsible)
        {
            if (string.IsNullOrWhiteSpace(responsible))
                return true;

            var value = responsible.Trim();
            return value == "-"
                   || value == "-1"
                   || value.Equals("Не назначен", StringComparison.CurrentCultureIgnoreCase)
                   || value.Equals("unassigned", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("null", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
                duration = TimeSpan.Zero;

            var hours = (int)duration.TotalHours;
            return $"{hours:00}:{duration.Minutes:00}";
        }

        // Return issues for equipment filtered by ShowRepairs/ShowSetups
    }
}
