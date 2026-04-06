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
        public ObservableCollection<int> DayTimeline { get; set; } = new ObservableCollection<int>();
        public ObservableCollection<Models.TimelinePoint> DayTimelinePoints { get; set; } = new ObservableCollection<Models.TimelinePoint>();
        public ObservableCollection<Models.Annotation> Annotations { get; set; } = new ObservableCollection<Models.Annotation>();
        public ReactiveCommand<DateTime, Unit> ShowDayTimelineCommand { get; set; }

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
                ApplyFilter();
            }
        }

        private void ApplyFilter()
        {
            EquipmentCollection.Clear();
            var q = (SearchQuery ?? string.Empty).Trim();
            var filtered = string.IsNullOrEmpty(q)
                ? _allEquipment
                : _allEquipment.Where(e => ((e.Title ?? string.Empty) + " " + (e.InventoryNumber ?? string.Empty)).IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0).ToList();

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
            // load all equipment and sort by issue count descending
            var all = xmlDataDecoder.DecodeEquipment().ToList();
            all.ForEach(e => { /* ensure Issues collection is not null */ });
            _allEquipment = all.OrderByDescending(e => e.Issues?.Count ?? 0).ToList();
            EquipmentCollection = new ObservableCollection<EquipmentInfo>();
            ApplyFilter();

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

                // compute right panel summary for selected equipment
                AnalysisDate = DateTime.Now;
                // compute faults and downtime for today
                int faultsToday = 0;
                double totalDownMinutes = 0.0;
                var dateStart = AnalysisDate.Date;
                var dateEnd = dateStart.AddDays(1);
                if (equipment?.Issues != null)
                {
                    foreach (var issue in equipment.Issues)
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
                DowntimePercent = workPercent.ToString("0.0") + "%"; // label for 'Работа'
                DownPercent = downPercent.ToString("0.0") + "%"; // label for 'Простой'

                // average repair time across all issues
                string avgRepair = "0 мин";
                if (equipment?.Issues != null && equipment.Issues.Count > 0)
                {
                    double totalMinutes = equipment.Issues.Sum(it => (it.End - it.Start).TotalMinutes);
                    double avg = totalMinutes / equipment.Issues.Count;
                    avgRepair = Math.Round(avg) + " мин";
                }
                AvgRepairTime = avgRepair;

                if (equipment?.Issues != null)
                {
                    // increment index for each issue for every calendar day it spans
                    // this ensures faults that flow into the next day are counted on that day as well
                    foreach (var issue in equipment.Issues)
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

                    if (SelectedEquipment?.Issues == null)
                        return;

                    // collect overlap intervals in minutes (accuracy to 1 minute)
                    var intervals = new System.Collections.Generic.List<(int sMin, int eMin)>();

                    foreach (var issue in SelectedEquipment.Issues)
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
                        Annotations.Add(new Models.Annotation { Hour = sMin / 60.0, Description = desc, Responsible = resp, Duration = duration.ToString(@"hh\:mm"), Type = issue.Type });

                        // also mark hourly buckets (for compatibility)
                        int startHour = (int)Math.Floor(sMin / 60.0);
                        int endHour = (int)Math.Ceiling(eMin / 60.0);
                        startHour = Math.Clamp(startHour, 0, 23);
                        endHour = Math.Clamp(endHour, 0, 24);
                        for (int h = startHour; h < endHour; h++)
                            DayTimeline[h] = 1;
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

                    // reuse existing logic to build timeline for date
                    // keep hourly array for compatibility
                    DayTimeline.Clear();
                    for (int h = 0; h < 24; h++)
                        DayTimeline.Add(0);

                    DayTimelinePoints.Clear();
                    Annotations.Clear();

                    if (SelectedEquipment?.Issues == null)
                        return;

                    var intervals = new System.Collections.Generic.List<(int sMin, int eMin)>();
                    foreach (var issue in SelectedEquipment.Issues)
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

                        var duration = TimeSpan.FromMinutes(Math.Max(0, eMin - sMin));
                        var desc = issue.Description ?? string.Empty;
                        var resp = string.IsNullOrEmpty(issue.Responsible) ? "-" : issue.Responsible;
                        Annotations.Add(new Models.Annotation { Hour = sMin / 60.0, Description = desc, Responsible = resp, Duration = duration.ToString(@"hh\:mm"), Type = issue.Type });

                        int startHour = (int)Math.Floor(sMin / 60.0);
                        int endHour = (int)Math.Ceiling(eMin / 60.0);
                        startHour = Math.Clamp(startHour, 0, 23);
                        endHour = Math.Clamp(endHour, 0, 24);
                        for (int h = startHour; h < endHour; h++)
                            DayTimeline[h] = 1;
                    }

                    if (intervals.Count == 0)
                    {
                        DayTimelinePoints.Add(new Models.TimelinePoint { Hour = 0.0, Value = 0 });
                        DayTimelinePoints.Add(new Models.TimelinePoint { Hour = 24.0, Value = 0 });
                        return;
                    }

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

                    DayTimelinePoints.Add(new Models.TimelinePoint { Hour = 0.0, Value = 0 });
                    foreach (var m in merged)
                    {
                        DayTimelinePoints.Add(new Models.TimelinePoint { Hour = m.sMin / 60.0, Value = 1 });
                        DayTimelinePoints.Add(new Models.TimelinePoint { Hour = m.eMin / 60.0, Value = 0 });
                    }
                    DayTimelinePoints.Add(new Models.TimelinePoint { Hour = 24.0, Value = 0 });
                });
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
    }
}
