using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.Utility;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using System.Reactive;

namespace EquipmentFailureAnalysis.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollection<EquipmentInfo> EquipmentCollection { get; set; }
        // master list containing all equipment (unmodified)
        private System.Collections.Generic.List<EquipmentInfo> _masterEquipment = new System.Collections.Generic.List<EquipmentInfo>();
        // working list sorted/filtered according to search and type filters
        private System.Collections.Generic.List<EquipmentInfo> _allEquipment = new System.Collections.Generic.List<EquipmentInfo>();
        public ObservableCollection<DailyDowntimeIndex> DailyDowntimeIndexCollection { get; set; }
        public ObservableCollection<Models.MonthRow> MonthRows { get; set; } = new ObservableCollection<Models.MonthRow>();
        public ReactiveCommand<EquipmentInfo, Unit> LoadEquipmentCommand { get; }
        public ObservableCollection<int> DayHeaders { get; set; } = new ObservableCollection<int>();
        public ObservableCollection<int> DayHours { get; set; } = new ObservableCollection<int>();
        private EquipmentInfo? _selectedEquipment;
        public EquipmentInfo? SelectedEquipment
        {
            get => _selectedEquipment;
            set => this.RaiseAndSetIfChanged(ref _selectedEquipment, value);
        }

        // Heatmap cell size (pixels). Bindable so UI can scale heatmap automatically.
        private double _dayCellSize = 28.0;
        public double DayCellSize
        {
            get => _dayCellSize;
            set => this.RaiseAndSetIfChanged(ref _dayCellSize, value);
        }

        // Monthly stats for selected equipment
        private int _repairsLastMonth;
        public int RepairsLastMonth
        {
            get => _repairsLastMonth;
            set => this.RaiseAndSetIfChanged(ref _repairsLastMonth, value);
        }

        private int _setupsLastMonth;
        public int SetupsLastMonth
        {
            get => _setupsLastMonth;
            set => this.RaiseAndSetIfChanged(ref _setupsLastMonth, value);
        }

        private int _selectedDayRepairs;
        public int SelectedDayRepairs
        {
            get => _selectedDayRepairs;
            set => this.RaiseAndSetIfChanged(ref _selectedDayRepairs, value);
        }

        private int _selectedDaySetups;
        public int SelectedDaySetups
        {
            get => _selectedDaySetups;
            set => this.RaiseAndSetIfChanged(ref _selectedDaySetups, value);
        }
        public ObservableCollection<int> DayTimeline { get; set; } = new ObservableCollection<int>();
        private ObservableCollection<Models.TimelinePoint> _dayTimelinePoints = new ObservableCollection<Models.TimelinePoint>();
        public ObservableCollection<Models.TimelinePoint> DayTimelinePoints
        {
            get => _dayTimelinePoints;
            set => this.RaiseAndSetIfChanged(ref _dayTimelinePoints, value);
        }

        private ObservableCollection<Models.TimelinePoint> _repairsTimelinePoints = new ObservableCollection<Models.TimelinePoint>();
        public ObservableCollection<Models.TimelinePoint> RepairsTimelinePoints
        {
            get => _repairsTimelinePoints;
            set => this.RaiseAndSetIfChanged(ref _repairsTimelinePoints, value);
        }

        private ObservableCollection<Models.TimelinePoint> _setupsTimelinePoints = new ObservableCollection<Models.TimelinePoint>();
        public ObservableCollection<Models.TimelinePoint> SetupsTimelinePoints
        {
            get => _setupsTimelinePoints;
            set => this.RaiseAndSetIfChanged(ref _setupsTimelinePoints, value);
        }

        private ObservableCollection<Models.Annotation> _annotations = new ObservableCollection<Models.Annotation>();
        public ObservableCollection<Models.Annotation> Annotations
        {
            get => _annotations;
            set => this.RaiseAndSetIfChanged(ref _annotations, value);
        }
        public ReactiveCommand<DateTime, Unit> ShowDayTimelineCommand { get; set; }

        private bool _showRepairs = true;
        public bool ShowRepairs
        {
            get => _showRepairs;
            set
            {
                this.RaiseAndSetIfChanged(ref _showRepairs, value);
                // refresh selected equipment view
                if (SelectedEquipment != null)
                {
                    RefreshEquipmentView(SelectedEquipment);
                    BuildTimelineForDate(AnalysisDate, SelectedEquipment);
                }
            }
        }

        private bool _showSetups = true;
        public bool ShowSetups
        {
            get => _showSetups;
            set
            {
                this.RaiseAndSetIfChanged(ref _showSetups, value);
                if (SelectedEquipment != null)
                {
                    RefreshEquipmentView(SelectedEquipment);
                    BuildTimelineForDate(AnalysisDate, SelectedEquipment);
                }
            }
        }

        // UI info panel properties
        private DateTime _analysisDate = DateTime.Now;
        public DateTime AnalysisDate
        {
            get => _analysisDate;
            set => this.RaiseAndSetIfChanged(ref _analysisDate, value);
        }

        private int _faultsForDay;
        public int FaultsForDay
        {
            get => _faultsForDay;
            set => this.RaiseAndSetIfChanged(ref _faultsForDay, value);
        }

        private string _downtimePercent = "0%";
        public string DowntimePercent
        {
            get => _downtimePercent;
            set => this.RaiseAndSetIfChanged(ref _downtimePercent, value);
        }

        private string _downPercent = "0%";
        public string DownPercent
        {
            get => _downPercent;
            set => this.RaiseAndSetIfChanged(ref _downPercent, value);
        }

        private double _workPercent = 0.0;
        public double WorkPercent
        {
            get => _workPercent;
            set => this.RaiseAndSetIfChanged(ref _workPercent, value);
        }

        private string _avgRepairTime = "0 мин";
        public string AvgRepairTime
        {
            get => _avgRepairTime;
            set => this.RaiseAndSetIfChanged(ref _avgRepairTime, value);
        }

        private string _searchQuery = string.Empty;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                this.RaiseAndSetIfChanged(ref _searchQuery, value);

                var q = (value ?? string.Empty).Trim();
                if (_allEquipment.Any(e =>
                    string.Equals(e.Title ?? string.Empty, q, StringComparison.CurrentCultureIgnoreCase) ||
                    string.Equals(e.ToString(), q, StringComparison.CurrentCultureIgnoreCase)))
                {
                    return;
                }

                ApplyFilter();
            }
        }

        private bool _isEquipmentSearchOpen;
        public bool IsEquipmentSearchOpen
        {
            get => _isEquipmentSearchOpen;
            set => this.RaiseAndSetIfChanged(ref _isEquipmentSearchOpen, value);
        }

        public ObservableCollection<string> IssueTypeFilters { get; } = new ObservableCollection<string>
        {
            "Все позиции",
            "Ремонты",
            "Настройки"
        };

        private string _selectedIssueTypeFilter = "Все позиции";
        public string SelectedIssueTypeFilter
        {
            get => _selectedIssueTypeFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedIssueTypeFilter, value);

                if (value == "Ремонты")
                {
                    ShowRepairs = true;
                    ShowSetups = false;
                }
                else if (value == "Настройки")
                {
                    ShowRepairs = false;
                    ShowSetups = true;
                }
                else
                {
                    ShowRepairs = true;
                    ShowSetups = true;
                }
            }
        }

        private EquipmentInfo? _selectedEquipmentFromSearch;
        public EquipmentInfo? SelectedEquipmentFromSearch
        {
            get => _selectedEquipmentFromSearch;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedEquipmentFromSearch, value);
                if (value != null)
                {
                    LoadEquipmentCommand.Execute(value).Subscribe();
                    IsEquipmentSearchOpen = false;
                }
            }
        }

        private void ApplyFilter()
        {
            EquipmentCollection.Clear();
            var q = (SearchQuery ?? string.Empty).Trim();
            var filtered = string.IsNullOrEmpty(q)
                ? _allEquipment
                : _allEquipment.Where(e =>
                    (e.Title?.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false) ||
                    (e.InventoryNumber?.Contains(q, StringComparison.CurrentCultureIgnoreCase) ?? false));

            foreach (var e in filtered)
                EquipmentCollection.Add(e);
        }

        public MainWindowViewModel()
        {
            // prepare day headers 1..31 for the heatmap top row
            for (int i = 1; i <= 31; i++)
            {
                DayHeaders.Add(i);
            }
            // prepare hours 0..23 for timeline labels
            for (int h = 0; h < 24; h++)
                DayHours.Add(h);
            XmlDataDecoder xmlDataDecoder = new XmlDataDecoder();
            // load all equipment and set master list
            var all = xmlDataDecoder.DecodeEquipment().ToList();
            all.ForEach(e => { /* ensure Issues collection is not null */ });
            _masterEquipment = all;
            // initialize collection before applying filters (prevents null refs)
            EquipmentCollection = new ObservableCollection<EquipmentInfo>();
            // apply initial type filter and sort
            ApplyTypeFilterAndSort();

            // DayCellSize will be adjusted by view to fit available area

            // sort commands
            SortIssuesAscCommand = ReactiveCommand.Create(() =>
            {
                _allEquipment = _masterEquipment.OrderBy(e => e.Issues?.Count ?? 0).ToList();
                ApplyFilter();
            });
            SortIssuesDescCommand = ReactiveCommand.Create(() =>
            {
                _allEquipment = _masterEquipment.OrderByDescending(e => e.Issues?.Count ?? 0).ToList();
                ApplyFilter();
            });

            // start with empty daily index collection; it will be populated when the user clicks an equipment button
            DailyDowntimeIndexCollection = new ObservableCollection<DailyDowntimeIndex>();

            // Command that fills DailyDowntimeIndexCollection for the selected equipment
            LoadEquipmentCommand = ReactiveCommand.Create<EquipmentInfo>(equipment =>
            {
                DailyDowntimeIndexCollection.Clear();

                for (int i = 0; i < 365; i++)
                {
                    DailyDowntimeIndexCollection.Add(new DailyDowntimeIndex
                    {
                        Day = DateTime.Now.AddDays(-i),
                        Index = 0
                    });
                }

                // mark selection
                foreach (var eq in EquipmentCollection)
                    eq.IsSelected = false;
                SelectedEquipment = equipment;
                if (SelectedEquipment != null)
                    SelectedEquipment.IsSelected = true;
                IsEquipmentSearchOpen = false;

                // compute right panel summary for selected equipment
                AnalysisDate = DateTime.Now;
                // compute faults and downtime for today (respecting type filters)
                int faultsToday = 0;
                double totalDownMinutes = 0.0;
                var dateStart = AnalysisDate.Date;
                var dateEnd = dateStart.AddDays(1);
                var filteredIssues = GetFilteredIssues(equipment).ToList();

            // compute monthly repair/setup counts (last 30 days) based on raw issue types
            var since = DateTime.Now.Date.AddDays(-30);
            RepairsLastMonth = equipment.Issues.Count(i => i.Type == IssueType.Ремонт && i.Start.Date >= since);
            SetupsLastMonth = equipment.Issues.Count(i => i.Type == IssueType.Настройка && i.Start.Date >= since);

            // compute selected-day counts for today by default
            try
            {
                var selStart = DateTime.Now.Date;
                var selEnd = selStart.AddDays(1);
                SelectedDayRepairs = equipment.Issues.Count(i => i.Type == IssueType.Ремонт && i.End > selStart && i.Start < selEnd);
                SelectedDaySetups = equipment.Issues.Count(i => i.Type == IssueType.Настройка && i.End > selStart && i.Start < selEnd);
            }
            catch { }
                foreach (var issue in filteredIssues)
                {
                    var overlapStart = issue.Start < dateStart ? dateStart : issue.Start;
                    var overlapEnd = issue.End > dateEnd ? dateEnd : issue.End;
                    if (overlapEnd <= overlapStart)
                        continue;
                    faultsToday++;
                    totalDownMinutes += (overlapEnd - overlapStart).TotalMinutes;
                }
                FaultsForDay = faultsToday;
                double downPercent = Math.Min(100.0, (totalDownMinutes / (24.0 * 60.0)) * 100.0);
                double workPercent = 100.0 - downPercent;
                WorkPercent = workPercent;
                DowntimePercent = workPercent.ToString("0.0") + "%"; // label for 'Работа'
                DownPercent = downPercent.ToString("0.0") + "%"; // label for 'Простой'

                // average repair time across filtered issues
                string avgRepair = "0 мин";
                if (filteredIssues.Count > 0)
                {
                    double totalMinutes = filteredIssues.Sum(it => (it.End - it.Start).TotalMinutes);
                    double avg = totalMinutes / filteredIssues.Count;
                    avgRepair = Math.Round(avg) + " мин";
                }
                AvgRepairTime = avgRepair;

                if (filteredIssues != null)
                {
                    // increment index for each issue for every calendar day it spans
                    // this ensures faults that flow into the next day are counted on that day as well
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

                // prepare command to show timeline for a specific day
                ShowDayTimelineCommand = ReactiveCommand.Create<DateTime>(date =>
                {
                    // Update right-panel summary to reflect selected date
                    AnalysisDate = date.Date;
                    // compute faults and downtime for the selected date
                    int faultsToday = 0;
                    double totalDownMinutes = 0.0;
                    var dateStart = date.Date;
                    var dateEnd = dateStart.AddDays(1);
                    if (SelectedEquipment?.Issues != null)
                    {
                        foreach (var issue in SelectedEquipment.Issues)
                        {
                            var overlapStart = issue.Start < dateStart ? dateStart : issue.Start;
                            var overlapEnd = issue.End > dateEnd ? dateEnd : issue.End;
                            if (overlapEnd <= overlapStart)
                                continue;
                            faultsToday++;
                            totalDownMinutes += (overlapEnd - overlapStart).TotalMinutes;
                        }
                    }
                    FaultsForDay = faultsToday;
                    double downPercent = Math.Min(100.0, (totalDownMinutes / (24.0 * 60.0)) * 100.0);
                    double workPercent = 100.0 - downPercent;
                    WorkPercent = workPercent;
                    DowntimePercent = workPercent.ToString("0.0") + "%";
                    DownPercent = downPercent.ToString("0.0") + "%";
                    // average repair time for issues on this day
                    string avgRepair = "0 мин";
                    if (faultsToday > 0)
                    {
                        double avg = totalDownMinutes / faultsToday;
                        avgRepair = Math.Round(avg) + " мин";
                    }
                    AvgRepairTime = avgRepair;

                    // keep hourly array for compatibility
                    DayTimeline.Clear();
                    for (int h = 0; h < 24; h++)
                        DayTimeline.Add(0);

                    DayTimelinePoints.Clear();
                    Annotations.Clear();
                    var dayAnnotations = new System.Collections.Generic.List<Models.Annotation>();

                    var selIssues = GetFilteredIssues(SelectedEquipment);
                    if (selIssues == null || selIssues.Count() == 0)
                        return;

                    // collect overlap intervals in minutes (accuracy to 1 minute)
                    var intervals = new System.Collections.Generic.List<(int sMin, int eMin)>();

                    foreach (var issue in selIssues)
                    {
                        var issueStart = issue.Start;
                        var issueEnd = issue.End;
                        var dayStart = date.Date;
                        var dayEnd = dayStart.AddDays(1);

                        var overlapStart = issueStart < dayStart ? dayStart : issueStart;
                        var overlapEnd = issueEnd > dayEnd ? dayEnd : issueEnd;
                        if (overlapEnd <= overlapStart)
                            continue;

                        int sMin = (int)Math.Max(0, Math.Floor((overlapStart - dayStart).TotalMinutes));
                        int eMin = (int)Math.Min(24 * 60, Math.Ceiling((overlapEnd - dayStart).TotalMinutes));
                        intervals.Add((sMin, eMin));

                        // add annotation at the start of the overlap
                        var duration = TimeSpan.FromMinutes(Math.Max(0, eMin - sMin));
                        var desc = issue.Description ?? string.Empty;
                        var resp = string.IsNullOrEmpty(issue.Responsible) ? "-" : issue.Responsible;
                        dayAnnotations.Add(new Models.Annotation { Hour = sMin / 60.0, Description = desc, Responsible = resp, Duration = duration.ToString(@"hh\:mm"), Type = issue.Type });

                        // also mark hourly buckets (for compatibility)
                        int startHour = (int)Math.Floor(sMin / 60.0);
                        int endHour = (int)Math.Ceiling(eMin / 60.0);
                        startHour = Math.Clamp(startHour, 0, 23);
                        endHour = Math.Clamp(endHour, 0, 24);
                        for (int h = startHour; h < endHour; h++)
                            DayTimeline[h] = 1;
                    }

                    foreach (var annotation in dayAnnotations
                        .OrderByDescending(a => TimeSpan.TryParse(a.Duration, out var parsed) ? parsed : TimeSpan.Zero))
                    {
                        Annotations.Add(annotation);
                    }

                    if (intervals.Count == 0)
                    {
                        // no issues, create flat 0..24
                        DayTimelinePoints.Add(new Models.TimelinePoint { Hour = 0.0, Value = 0 });
                        DayTimelinePoints.Add(new Models.TimelinePoint { Hour = 24.0, Value = 0 });
                        return;
                    }

                    // merge intervals (in minutes)
                    intervals.Sort((a, b) => a.sMin.CompareTo(b.sMin));
                    var merged = new System.Collections.Generic.List<(int sMin, int eMin)>();
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

                    // build timeline points with minute accuracy
                    DayTimelinePoints.Add(new Models.TimelinePoint { Hour = 0.0, Value = 0 });
                    foreach (var m in merged)
                    {
                        DayTimelinePoints.Add(new Models.TimelinePoint { Hour = m.sMin / 60.0, Value = 1 });
                        DayTimelinePoints.Add(new Models.TimelinePoint { Hour = m.eMin / 60.0, Value = 0 });
                    }
                    DayTimelinePoints.Add(new Models.TimelinePoint { Hour = 24.0, Value = 0 });
                });
                }

                // build month rows for calendar year (January..December)
                MonthRows.Clear();
                var year = DateTime.Now.Year;
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

                    // create fixed 31 columns so header days align with buttons
                    for (int d = 1; d <= 31; d++)
                    {
                        var isValid = d <= daysInMonth;
                        var cell = new Models.DayCell { DayNumber = d, Index = 0, IsValid = isValid };
                        if (isValid)
                        {
                            // set valid date and find corresponding date in DailyDowntimeIndexCollection
                            cell.Date = new DateTime(monthDate.Year, monthDate.Month, d);
                            var entry = DailyDowntimeIndexCollection.FirstOrDefault(x => x.Day.Date == cell.Date.Date);
                            if (entry != null)
                                cell.Index = entry.Index;
                        }
                        monthRow.Days.Add(cell);
                    }

                    MonthRows.Add(monthRow);
                }

                // ensure ShowDayTimelineCommand still targets selected analysis date when a cell is clicked
                ShowDayTimelineCommand = ReactiveCommand.Create<DateTime>(date => BuildTimelineForDate(date, SelectedEquipment));
            });

            // Preselect first equipment (if exists) and load its data + today's timeline
            if (EquipmentCollection.Count > 0)
            {
                var first = EquipmentCollection[0];
                LoadEquipmentCommand.Execute(first).Subscribe(_ =>
                {
                    if (ShowDayTimelineCommand != null)
                        ShowDayTimelineCommand.Execute(DateTime.Now.Date).Subscribe();
                });
            }
        }

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
            // keep left column ordering by total issues
            _allEquipment = _masterEquipment.OrderByDescending(e => e.Issues?.Count ?? 0).ToList();
            EquipmentCollection.Clear();
            ApplyFilter();

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
        }

        // Return issues for equipment filtered by ShowRepairs/ShowSetups
        private System.Collections.Generic.IEnumerable<Issue> GetFilteredIssues(EquipmentInfo? equipment)
        {
            if (equipment == null)
                return System.Linq.Enumerable.Empty<Issue>();

            return equipment.Issues.Where(i =>
                (ShowRepairs && i.Type == IssueType.Ремонт) ||
                (ShowSetups && i.Type == IssueType.Настройка));
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
                annList.Add(new Models.Annotation { Hour = sMin / 60.0, Description = desc, Responsible = resp, Duration = duration.ToString(@"hh\:mm"), Type = issue.Type });
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
                DowntimePercent = workPercent.ToString("0.0") + "%";
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
