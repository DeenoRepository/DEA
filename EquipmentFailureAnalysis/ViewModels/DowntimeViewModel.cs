using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Text;
using EquipmentFailureAnalysis.Models;

namespace EquipmentFailureAnalysis.ViewModels
{
    public sealed class DowntimeViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel _shell;
        private bool _isRefreshingFilters;

        private DateTime _downtimeAnalysisDate = DateTime.Now.Date;
        private string _selectedDowntimeIssueTypeFilter = "\u0412\u0441\u0435 \u0442\u0438\u043f\u044b";
        private string _selectedDowntimeStatusFilter = "\u0412\u0441\u0435 \u0441\u0442\u0430\u0442\u0443\u0441\u044b";
        private string _selectedDowntimeResponsibleFilter = "\u0412\u0441\u0435 \u043e\u0442\u0432\u0435\u0442\u0441\u0442\u0432\u0435\u043d\u043d\u044b\u0435";
        private string _selectedDowntimeSubdivisionFilter = "\u0412\u0441\u0435 \u0433\u0440\u0443\u043f\u043f\u044b";
        private string _downtimeEquipmentSearchQuery = string.Empty;
        private int _downtimeAffectedEquipmentCount;
        private int _downtimeTotalIssues;
        private int _downtimeRepairsCount;
        private int _downtimeSetupsCount;
        private double _downtimeAffectedSharePercent;
        private string _downtimeTotalDuration = "00:00";
        private string _downtimeAvgIssuesPerEquipment = "0.0";
        private string _downtimePeakHour = "-";
        private string _downtimeTopEquipment = "-";
        private int _downtimeTopEquipmentIssues;

        public DowntimeViewModel(MainWindowViewModel shell)
        {
            _shell = shell;

            DowntimeIssueTypeFilters.Add("\u0412\u0441\u0435 \u0442\u0438\u043f\u044b");
            DowntimeIssueTypeFilters.Add("\u0420\u0435\u043c\u043e\u043d\u0442\u044b");
            DowntimeIssueTypeFilters.Add("\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438");
            DowntimeStatusFilters.Add("\u0412\u0441\u0435 \u0441\u0442\u0430\u0442\u0443\u0441\u044b");
            DowntimeStatusFilters.Add("\u0412 \u043f\u0440\u043e\u0446\u0435\u0441\u0441\u0435");
            DowntimeStatusFilters.Add("\u0417\u0430\u0432\u0435\u0440\u0448\u0435\u043d\u0430");

            ResetUniversalFiltersCommand = ReactiveCommand.Create(ResetUniversalFilters);
            ShowDowntimeDayCommand = ReactiveCommand.Create<DateTime>(date =>
            {
                BuildDayEquipmentRows(_shell.GetEquipmentForReports(), date);
            });
        }

        public ObservableCollection<string> DowntimeIssueTypeFilters { get; } = new();
        public ObservableCollection<string> DowntimeStatusFilters { get; } = new();
        public ObservableCollection<string> DowntimeResponsibleFilters { get; } = new();
        public ObservableCollection<string> DowntimeSubdivisionFilters { get; } = new();
        public ObservableCollection<MonthRow> DowntimeMonthRows { get; } = new();
        public ObservableCollection<DowntimeEquipmentRow> DowntimeDayEquipmentRows { get; } = new();

        public string DowntimeHeatmapYearLabel
        {
            get
            {
                if (DowntimeMonthRows == null || DowntimeMonthRows.Count == 0) return "Год: —";
                var first = DowntimeMonthRows.First();
                var last = DowntimeMonthRows.Last();
                if (first.Year == last.Year) return $"Год: {first.Year}";
                var firstShort = System.Globalization.CultureInfo.GetCultureInfo("ru-RU")
                    .DateTimeFormat.GetAbbreviatedMonthName(first.Month);
                var lastShort = System.Globalization.CultureInfo.GetCultureInfo("ru-RU")
                    .DateTimeFormat.GetAbbreviatedMonthName(last.Month);
                firstShort = char.ToUpper(firstShort[0]) + firstShort.Substring(1);
                lastShort = char.ToUpper(lastShort[0]) + lastShort.Substring(1);
                return $"{firstShort} {first.Year} — {lastShort} {last.Year}";
            }
        }

        public ReactiveCommand<Unit, Unit> ResetUniversalFiltersCommand { get; }
        public ReactiveCommand<DateTime, Unit> ShowDowntimeDayCommand { get; }

        public int HeatmapColorMin
        {
            get => _shell.HeatmapColorMin;
            set => _shell.HeatmapColorMin = value;
        }

        public int HeatmapColorMax
        {
            get => _shell.HeatmapColorMax;
            set => _shell.HeatmapColorMax = value;
        }

        public ObservableCollection<int> DayHeaders => _shell.DayHeaders;

        public double DayCellSize
        {
            get => _shell.DayCellSize;
            set => _shell.DayCellSize = value;
        }

        public string SelectedDowntimeIssueTypeFilter
        {
            get => _selectedDowntimeIssueTypeFilter;
            set
            {
                if (string.Equals(_selectedDowntimeIssueTypeFilter, value, StringComparison.CurrentCulture))
                    return;

                this.RaiseAndSetIfChanged(ref _selectedDowntimeIssueTypeFilter, value);
                OnFiltersChanged();
            }
        }

        public string SelectedDowntimeStatusFilter
        {
            get => _selectedDowntimeStatusFilter;
            set
            {
                if (string.Equals(_selectedDowntimeStatusFilter, value, StringComparison.CurrentCulture))
                    return;

                this.RaiseAndSetIfChanged(ref _selectedDowntimeStatusFilter, value);
                OnFiltersChanged();
            }
        }

        public string SelectedDowntimeResponsibleFilter
        {
            get => _selectedDowntimeResponsibleFilter;
            set
            {
                if (string.Equals(_selectedDowntimeResponsibleFilter, value, StringComparison.CurrentCulture))
                    return;

                this.RaiseAndSetIfChanged(ref _selectedDowntimeResponsibleFilter, value);
                OnFiltersChanged();
            }
        }

        public string SelectedDowntimeSubdivisionFilter
        {
            get => _selectedDowntimeSubdivisionFilter;
            set
            {
                if (string.Equals(_selectedDowntimeSubdivisionFilter, value, StringComparison.CurrentCulture))
                    return;

                this.RaiseAndSetIfChanged(ref _selectedDowntimeSubdivisionFilter, value);
                OnFiltersChanged();
            }
        }

        public string DowntimeEquipmentSearchQuery
        {
            get => _downtimeEquipmentSearchQuery;
            set
            {
                if (string.Equals(_downtimeEquipmentSearchQuery, value, StringComparison.CurrentCulture))
                    return;

                this.RaiseAndSetIfChanged(ref _downtimeEquipmentSearchQuery, value);
                OnFiltersChanged();
            }
        }

        public DateTime DowntimeAnalysisDate
        {
            get => _downtimeAnalysisDate;
            set => this.RaiseAndSetIfChanged(ref _downtimeAnalysisDate, value.Date);
        }

        public double DowntimeAffectedSharePercent
        {
            get => _downtimeAffectedSharePercent;
            set => this.RaiseAndSetIfChanged(ref _downtimeAffectedSharePercent, value);
        }

        public int DowntimeAffectedEquipmentCount
        {
            get => _downtimeAffectedEquipmentCount;
            set => this.RaiseAndSetIfChanged(ref _downtimeAffectedEquipmentCount, value);
        }

        public int DowntimeTotalIssues
        {
            get => _downtimeTotalIssues;
            set
            {
                this.RaiseAndSetIfChanged(ref _downtimeTotalIssues, value);
                this.RaisePropertyChanged(nameof(HasDowntimeIssues));
            }
        }

        public int DowntimeRepairsCount
        {
            get => _downtimeRepairsCount;
            set => this.RaiseAndSetIfChanged(ref _downtimeRepairsCount, value);
        }

        public int DowntimeSetupsCount
        {
            get => _downtimeSetupsCount;
            set => this.RaiseAndSetIfChanged(ref _downtimeSetupsCount, value);
        }

        public string DowntimeTotalDuration
        {
            get => _downtimeTotalDuration;
            set => this.RaiseAndSetIfChanged(ref _downtimeTotalDuration, value ?? "00:00");
        }

        public string DowntimeAvgIssuesPerEquipment
        {
            get => _downtimeAvgIssuesPerEquipment;
            set => this.RaiseAndSetIfChanged(ref _downtimeAvgIssuesPerEquipment, value ?? "0.0");
        }

        public string DowntimePeakHour
        {
            get => _downtimePeakHour;
            set => this.RaiseAndSetIfChanged(ref _downtimePeakHour, value ?? "-");
        }

        public string DowntimeTopEquipment
        {
            get => _downtimeTopEquipment;
            set => this.RaiseAndSetIfChanged(ref _downtimeTopEquipment, value ?? "-");
        }

        public int DowntimeTopEquipmentIssues
        {
            get => _downtimeTopEquipmentIssues;
            set => this.RaiseAndSetIfChanged(ref _downtimeTopEquipmentIssues, value);
        }

        public bool HasDowntimeIssues => DowntimeTotalIssues > 0;

        public void Refresh(IReadOnlyCollection<EquipmentInfo> sourceEquipment, DateTime date)
        {
            BuildHeatmap(sourceEquipment);
            BuildDayEquipmentRows(sourceEquipment, date);
        }

        public void BuildHeatmap(IReadOnlyCollection<EquipmentInfo> sourceEquipment)
        {
            DowntimeMonthRows.Clear();
            var filteredEquipment = FilterDowntimeEquipmentByQuery(sourceEquipment);

            var allIssues = filteredEquipment.SelectMany(eq => eq.Issues).ToList();
            DateTime minDate = DateTime.Today.AddMonths(-3);
            DateTime maxDate = DateTime.Today;

            if (allIssues.Count > 0)
            {
                var earliest = allIssues.Min(i => i.Start);
                var latest = allIssues.Max(i => i.Start);
                if (earliest < minDate) minDate = earliest;
                if (latest > maxDate) maxDate = latest;
            }

            var current = new DateTime(minDate.Year, minDate.Month, 1);
            var endLimit = new DateTime(maxDate.Year, maxDate.Month, 1);

            while (current <= endLimit)
            {
                var monthDate = current;
                var daysInMonth = DateTime.DaysInMonth(monthDate.Year, monthDate.Month);
                var name = monthDate.ToString("MMMM yyyy");
                if (!string.IsNullOrEmpty(name))
                    name = char.ToUpper(name[0]) + name.Substring(1);

                var monthRow = new MonthRow
                {
                    Month = monthDate.Month,
                    Year = monthDate.Year,
                    MonthName = name
                };

                int dowOffset = ((int)monthDate.DayOfWeek + 6) % 7; // Monday-based index (0-6)
                for (int i = 0; i < dowOffset; i++)
                {
                    monthRow.Days.Add(new DayCell { DayNumber = 0, Index = 0, IsValid = false });
                }

                for (int d = 1; d <= daysInMonth; d++)
                {
                    var cell = new DayCell { DayNumber = d, Index = 0, IsValid = true };
                    var day = new DateTime(monthDate.Year, monthDate.Month, d);
                    var dayEnd = day.AddDays(1);
                    cell.Date = day;
                    cell.Index = filteredEquipment.Count(eq => GetDowntimeFilteredIssues(eq, day, dayEnd).Any());
                    monthRow.Days.Add(cell);
                }

                while (monthRow.Days.Count < 42)
                {
                    monthRow.Days.Add(new DayCell { DayNumber = 0, Index = 0, IsValid = false });
                }

                DowntimeMonthRows.Add(monthRow);
                current = current.AddMonths(1);
            }
            this.RaisePropertyChanged(nameof(DowntimeHeatmapYearLabel));
        }

        public void BuildDayEquipmentRows(IReadOnlyCollection<EquipmentInfo> sourceEquipment, DateTime date)
        {
            DowntimeAnalysisDate = date.Date;
            DowntimeDayEquipmentRows.Clear();

            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);

            var rows = new List<DowntimeEquipmentRow>();
            int totalIssues = 0;
            int totalRepairs = 0;
            int totalSetups = 0;
            double totalMergedDownMinutes = 0.0;
            var affectedByHour = new int[24];

            foreach (var equipment in FilterDowntimeEquipmentByQuery(sourceEquipment))
            {
                var issuesForDay = GetDowntimeFilteredIssues(equipment, dayStart, dayEnd).ToList();
                if (issuesForDay.Count == 0)
                    continue;

                totalIssues += issuesForDay.Count;
                totalRepairs += issuesForDay.Count(i => i.Type == IssueType.Ремонт);
                totalSetups += issuesForDay.Count(i => i.Type == IssueType.Настройка);

                var intervals = new List<(int sMin, int eMin)>();
                var repairsIntervals = new List<(int sMin, int eMin)>();
                var setupsIntervals = new List<(int sMin, int eMin)>();
                var rowAnnotations = new List<Annotation>();

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

                    rowAnnotations.Add(new Annotation
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
                        JiraIssueKey = issue.JiraIssueKey ?? string.Empty,
                        Reporter = issue.Reporter ?? string.Empty,
                        Comments = issue.Comments ?? string.Empty,
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

                rows.Add(new DowntimeEquipmentRow
                {
                    Equipment = equipment,
                    Title = equipment.Title,
                    InventoryNumber = equipment.InventoryNumber ?? "-",
                    IssuesCount = issuesForDay.Count,
                    TimelinePoints = new ObservableCollection<TimelinePoint>(BuildTimelinePoints(merged)),
                    RepairsTimelinePoints = new ObservableCollection<TimelinePoint>(BuildTimelinePoints(repairsMerged)),
                    SetupsTimelinePoints = new ObservableCollection<TimelinePoint>(BuildTimelinePoints(setupsMerged)),
                    Annotations = new ObservableCollection<Annotation>(
                        rowAnnotations.OrderBy(a => a.StartHour).ThenBy(a => a.EndHour))
                });
            }

            foreach (var row in rows.OrderByDescending(r => r.IssuesCount))
                DowntimeDayEquipmentRows.Add(row);

            DowntimeAffectedEquipmentCount = rows.Count;
            DowntimeTotalIssues = totalIssues;
            DowntimeRepairsCount = totalRepairs;
            DowntimeSetupsCount = totalSetups;
            DowntimeAffectedSharePercent = sourceEquipment.Count == 0 ? 0.0 : rows.Count * 100.0 / sourceEquipment.Count;
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

        public void RebuildResponsibleFilters(IReadOnlyCollection<EquipmentInfo> sourceEquipment)
        {
            var previous = SelectedDowntimeResponsibleFilter;
            DowntimeResponsibleFilters.Clear();
            DowntimeResponsibleFilters.Add("\u0412\u0441\u0435 \u043e\u0442\u0432\u0435\u0442\u0441\u0442\u0432\u0435\u043d\u043d\u044b\u0435");
            DowntimeResponsibleFilters.Add("\u0411\u0435\u0437 \u043e\u0442\u0432\u0435\u0442\u0441\u0442\u0432\u0435\u043d\u043d\u043e\u0433\u043e");

            foreach (var responsible in sourceEquipment
                .SelectMany(eq => eq.Issues)
                .Select(i => i.Responsible?.Trim())
                .Where(r => !string.IsNullOrWhiteSpace(r) && !IsUnassignedResponsible(r))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(r => r, StringComparer.CurrentCultureIgnoreCase))
            {
                DowntimeResponsibleFilters.Add(responsible!);
            }

            _isRefreshingFilters = true;
            SelectedDowntimeResponsibleFilter = DowntimeResponsibleFilters.Contains(previous)
                ? previous
                : "\u0412\u0441\u0435 \u043e\u0442\u0432\u0435\u0442\u0441\u0442\u0432\u0435\u043d\u043d\u044b\u0435";
            _isRefreshingFilters = false;
        }

        public void RebuildSubdivisionFilters(IReadOnlyCollection<EquipmentInfo> sourceEquipment)
        {
            var previous = SelectedDowntimeSubdivisionFilter;
            DowntimeSubdivisionFilters.Clear();
            DowntimeSubdivisionFilters.Add("\u0412\u0441\u0435 \u0433\u0440\u0443\u043f\u043f\u044b");

            foreach (var subdivision in sourceEquipment
                .Select(eq => eq.Subdivision?.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase))
            {
                DowntimeSubdivisionFilters.Add(subdivision!);
            }

            _isRefreshingFilters = true;
            SelectedDowntimeSubdivisionFilter = DowntimeSubdivisionFilters.Contains(previous)
                ? previous
                : "\u0412\u0441\u0435 \u0433\u0440\u0443\u043f\u043f\u044b";
            _isRefreshingFilters = false;
        }

        private void OnFiltersChanged()
        {
            if (_isRefreshingFilters)
                return;

            Refresh(_shell.GetEquipmentForReports(), DowntimeAnalysisDate);
            _shell.HandleDowntimeFilterChanged();
        }

        private void ResetUniversalFilters()
        {
            _isRefreshingFilters = true;
            SelectedDowntimeIssueTypeFilter = "\u0412\u0441\u0435 \u0442\u0438\u043f\u044b";
            SelectedDowntimeStatusFilter = "\u0412\u0441\u0435 \u0441\u0442\u0430\u0442\u0443\u0441\u044b";
            SelectedDowntimeResponsibleFilter = "\u0412\u0441\u0435 \u043e\u0442\u0432\u0435\u0442\u0441\u0442\u0432\u0435\u043d\u043d\u044b\u0435";
            SelectedDowntimeSubdivisionFilter = "\u0412\u0441\u0435 \u0433\u0440\u0443\u043f\u043f\u044b";
            DowntimeEquipmentSearchQuery = string.Empty;
            _isRefreshingFilters = false;

            // Signal to view for toast feedback
            _shell.FiltersResetCounter += 1;
            OnFiltersChanged();
        }

        private bool MatchesDowntimeSubdivision(EquipmentInfo equipment)
        {
            if (string.Equals(SelectedDowntimeSubdivisionFilter, "\u0412\u0441\u0435 \u0433\u0440\u0443\u043f\u043f\u044b", StringComparison.CurrentCultureIgnoreCase))
                return true;

            return string.Equals(equipment.Subdivision?.Trim(), SelectedDowntimeSubdivisionFilter, StringComparison.CurrentCultureIgnoreCase);
        }

        private List<EquipmentInfo> FilterDowntimeEquipmentByQuery(IEnumerable<EquipmentInfo> source)
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

        private IEnumerable<Issue> GetDowntimeFilteredIssues(EquipmentInfo equipment, DateTime start, DateTime end)
        {
            if (!MatchesDowntimeSubdivision(equipment))
                return Enumerable.Empty<Issue>();

            var source = equipment.Issues.Where(issue => issue.End > start && issue.Start < end);

            source = SelectedDowntimeIssueTypeFilter switch
            {
                "\u0420\u0435\u043c\u043e\u043d\u0442\u044b" => source.Where(i => i.Type == IssueType.Ремонт),
                "\u041d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0438" => source.Where(i => i.Type == IssueType.Настройка),
                _ => source
            };

            source = SelectedDowntimeStatusFilter switch
            {
                "\u0412 \u043f\u0440\u043e\u0446\u0435\u0441\u0441\u0435" => source.Where(i => i.IsInProgress),
                "\u0417\u0430\u0432\u0435\u0440\u0448\u0435\u043d\u0430" => source.Where(i => !i.IsInProgress),
                _ => source
            };

            if (!string.Equals(SelectedDowntimeResponsibleFilter, "\u0412\u0441\u0435 \u043e\u0442\u0432\u0435\u0442\u0441\u0442\u0432\u0435\u043d\u043d\u044b\u0435", StringComparison.CurrentCultureIgnoreCase))
            {
                if (string.Equals(SelectedDowntimeResponsibleFilter, "\u0411\u0435\u0437 \u043e\u0442\u0432\u0435\u0442\u0441\u0442\u0432\u0435\u043d\u043d\u043e\u0433\u043e", StringComparison.CurrentCultureIgnoreCase))
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

        private static bool IsUnassignedResponsible(string? responsible)
        {
            if (string.IsNullOrWhiteSpace(responsible))
                return true;

            var normalized = responsible.Trim();
            return normalized == "-"
                || string.Equals(normalized, "\u043d/\u0434", StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(normalized, "\u043d\u0435 \u0443\u043a\u0430\u0437\u0430\u043d", StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(normalized, "\u0431\u0435\u0437 \u043e\u0442\u0432\u0435\u0442\u0441\u0442\u0432\u0435\u043d\u043d\u043e\u0433\u043e", StringComparison.CurrentCultureIgnoreCase);
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
                var chunked = new StringBuilder(token.Length + (token.Length / 16));
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

        private static List<(int sMin, int eMin)> MergeIntervals(List<(int sMin, int eMin)> intervals)
        {
            var merged = new List<(int sMin, int eMin)>();
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

        private static List<TimelinePoint> BuildTimelinePoints(List<(int sMin, int eMin)> merged)
        {
            var points = new List<TimelinePoint>();

            if (merged.Count == 0)
            {
                points.Add(new TimelinePoint { Hour = 0.0, Value = 0 });
                points.Add(new TimelinePoint { Hour = 24.0, Value = 0 });
                return points;
            }

            int startValue = merged[0].sMin <= 0 ? 1 : 0;
            points.Add(new TimelinePoint { Hour = 0.0, Value = startValue });

            foreach (var m in merged)
            {
                if (m.sMin > 0)
                    points.Add(new TimelinePoint { Hour = m.sMin / 60.0, Value = 1 });

                if (m.eMin < 24 * 60)
                    points.Add(new TimelinePoint { Hour = m.eMin / 60.0, Value = 0 });
            }

            int endValue = merged[^1].eMin >= 24 * 60 ? 1 : 0;
            points.Add(new TimelinePoint { Hour = 24.0, Value = endValue });

            return points;
        }
    }
}
