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
    public partial class MainWindowViewModel : ViewModelBase
    {
        public DashboardViewModel Dashboard { get; }
        public DowntimeViewModel Downtime { get; }
        public ReportsViewModel Reports { get; }
        public SettingsViewModel Settings { get; }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        private sealed class EmployeeIssueProjection
        {
            public required EquipmentInfo Equipment { get; init; }
            public required Issue Issue { get; init; }
        }

        public ObservableCollection<EquipmentInfo> EquipmentCollection { get; set; }
        // master list containing all equipment (unmodified)
        private System.Collections.Generic.List<EquipmentInfo> _masterEquipment = new System.Collections.Generic.List<EquipmentInfo>();
        // working list sorted/filtered according to search and type filters
        private System.Collections.Generic.List<EquipmentInfo> _allEquipment = new System.Collections.Generic.List<EquipmentInfo>();

        public System.Collections.Generic.IReadOnlyCollection<EquipmentInfo> GetEquipmentForReports()
        {
            if (_masterEquipment != null && _masterEquipment.Count > 0)
                return _masterEquipment;

            return EquipmentCollection?.ToList() ?? new System.Collections.Generic.List<EquipmentInfo>();
        }

        public ObservableCollection<DailyDowntimeIndex> DailyDowntimeIndexCollection { get; set; }
        private string _statusMessage = "Готово.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public ObservableCollection<string> SyncLogs { get; } = new ObservableCollection<string>();

        public void AddStatusEvent(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var logEntry = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] {message.Trim()}";
            SyncLogs.Insert(0, logEntry);
            StatusMessage = $"[{DateTime.Now:HH:mm:ss}] {message.Trim()}";
        }

        private long _filtersResetCounter;
        /// <summary>Monotonically increasing counter; increments on every filter reset.
        /// Views observe it to show a confirmation toast.</summary>
        public long FiltersResetCounter
        {
            get => _filtersResetCounter;
            set => this.RaiseAndSetIfChanged(ref _filtersResetCounter, value);
        }

        public ObservableCollection<Models.MonthRow> MonthRows { get; set; } = new ObservableCollection<Models.MonthRow>();
        public ObservableCollection<Models.MonthRow> DowntimeMonthRows => Downtime.DowntimeMonthRows;
        public ObservableCollection<Models.DowntimeEquipmentRow> DowntimeDayEquipmentRows => Downtime.DowntimeDayEquipmentRows;

        public string HeatmapYearLabel
        {
            get
            {
                if (MonthRows == null || MonthRows.Count == 0) return "Год: —";
                var first = MonthRows.First();
                var last = MonthRows.Last();
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
        private ObservableCollection<Models.EmployeeAnalysisRow> _employeeAnalysisRows = new ObservableCollection<Models.EmployeeAnalysisRow>();
        public ObservableCollection<Models.EmployeeAnalysisRow> EmployeeAnalysisRows
        {
            get => _employeeAnalysisRows;
            set => this.RaiseAndSetIfChanged(ref _employeeAnalysisRows, value);
        }

        public ObservableCollection<Models.DashboardTrendPoint> DashboardMonthlyTrends
        {
            get => Dashboard.DashboardMonthlyTrends;
            set
            {
                Dashboard.DashboardMonthlyTrends = value;
                this.RaisePropertyChanged(nameof(DashboardMonthlyTrends));
            }
        }
        public ReactiveCommand<EquipmentInfo, Unit> LoadEquipmentCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetUniversalFiltersCommand => Downtime.ResetUniversalFiltersCommand;
        public ObservableCollection<int> DayHeaders { get; set; } = new ObservableCollection<int>();
        public ObservableCollection<int> DayHours { get; set; } = new ObservableCollection<int>();
        public ReactiveCommand<DateTime, Unit> ShowDowntimeDayCommand => Downtime.ShowDowntimeDayCommand;
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

        private const string FailureHeatmapOption = "Карта тепла неисправностей";
        private const string DowntimeHeatmapOption = "Карта тепла простоев";

        public ObservableCollection<string> HeatmapSettingOptions { get; } = new ObservableCollection<string>();

        private int _failureHeatmapColorMin = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.FailureHeatmapKey).Min;
        private int _failureHeatmapColorMax = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.FailureHeatmapKey).Max;
        private int _downtimeHeatmapColorMin = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.DowntimeHeatmapKey).Min;
        private int _downtimeHeatmapColorMax = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.DowntimeHeatmapKey).Max;

        private string _selectedHeatmapSetting = FailureHeatmapOption;
        public string SelectedHeatmapSetting
        {
            get => _selectedHeatmapSetting;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedHeatmapSetting, value);
                this.RaisePropertyChanged(nameof(HeatmapColorMin));
                this.RaisePropertyChanged(nameof(HeatmapColorMax));
            }
        }

        public int HeatmapColorMin
        {
            get => IsDowntimeHeatmapSelected ? _downtimeHeatmapColorMin : _failureHeatmapColorMin;
            set
            {
                var normalizedMin = Math.Max(0, value);
                var currentMax = IsDowntimeHeatmapSelected ? _downtimeHeatmapColorMax : _failureHeatmapColorMax;
                var normalizedMax = Math.Max(currentMax, normalizedMin + 1);

                var minChanged = IsDowntimeHeatmapSelected
                    ? normalizedMin != _downtimeHeatmapColorMin
                    : normalizedMin != _failureHeatmapColorMin;
                var maxChanged = IsDowntimeHeatmapSelected
                    ? normalizedMax != _downtimeHeatmapColorMax
                    : normalizedMax != _failureHeatmapColorMax;
                if (!minChanged && !maxChanged)
                    return;

                if (minChanged)
                {
                    if (IsDowntimeHeatmapSelected)
                        _downtimeHeatmapColorMin = normalizedMin;
                    else
                        _failureHeatmapColorMin = normalizedMin;
                }

                if (maxChanged)
                {
                    if (IsDowntimeHeatmapSelected)
                        _downtimeHeatmapColorMax = normalizedMax;
                    else
                        _failureHeatmapColorMax = normalizedMax;
                }

                this.RaisePropertyChanged(nameof(HeatmapColorMin));
                this.RaisePropertyChanged(nameof(HeatmapColorMax));
                ApplyHeatmapColorRange();
            }
        }

        public int HeatmapColorMax
        {
            get => IsDowntimeHeatmapSelected ? _downtimeHeatmapColorMax : _failureHeatmapColorMax;
            set
            {
                var normalizedMax = Math.Max(1, value);
                var currentMin = IsDowntimeHeatmapSelected ? _downtimeHeatmapColorMin : _failureHeatmapColorMin;
                var normalizedMin = Math.Min(currentMin, normalizedMax - 1);

                var maxChanged = IsDowntimeHeatmapSelected
                    ? normalizedMax != _downtimeHeatmapColorMax
                    : normalizedMax != _failureHeatmapColorMax;
                var minChanged = IsDowntimeHeatmapSelected
                    ? normalizedMin != _downtimeHeatmapColorMin
                    : normalizedMin != _failureHeatmapColorMin;
                if (!maxChanged && !minChanged)
                    return;

                if (maxChanged)
                {
                    if (IsDowntimeHeatmapSelected)
                        _downtimeHeatmapColorMax = normalizedMax;
                    else
                        _failureHeatmapColorMax = normalizedMax;
                }

                if (minChanged)
                {
                    if (IsDowntimeHeatmapSelected)
                        _downtimeHeatmapColorMin = normalizedMin;
                    else
                        _failureHeatmapColorMin = normalizedMin;
                }

                this.RaisePropertyChanged(nameof(HeatmapColorMin));
                this.RaisePropertyChanged(nameof(HeatmapColorMax));
                ApplyHeatmapColorRange();
            }
        }

        private bool IsDowntimeHeatmapSelected =>
            string.Equals(SelectedHeatmapSetting, DowntimeHeatmapOption, StringComparison.CurrentCultureIgnoreCase);

        public void SelectFailureHeatmapSettings()
        {
            SelectedHeatmapSetting = FailureHeatmapOption;
        }

        public void SelectDowntimeHeatmapSettings()
        {
            SelectedHeatmapSetting = DowntimeHeatmapOption;
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

        private Models.Annotation? _selectedTimelineAnnotation;
        public Models.Annotation? SelectedTimelineAnnotation
        {
            get => _selectedTimelineAnnotation;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedTimelineAnnotation, value);
                this.RaisePropertyChanged(nameof(SelectedTimelineHasData));
                this.RaisePropertyChanged(nameof(SelectedTimelineType));
                this.RaisePropertyChanged(nameof(SelectedTimelineResponsible));
                this.RaisePropertyChanged(nameof(SelectedTimelineStart));
                this.RaisePropertyChanged(nameof(SelectedTimelineEnd));
                this.RaisePropertyChanged(nameof(SelectedTimelineDuration));
                this.RaisePropertyChanged(nameof(SelectedTimelineDescription));
                this.RaisePropertyChanged(nameof(SelectedTimelineStatus));
                this.RaisePropertyChanged(nameof(SelectedTimelineJiraKey));
                this.RaisePropertyChanged(nameof(SelectedTimelineReporter));
                this.RaisePropertyChanged(nameof(SelectedTimelineComments));
                this.RaisePropertyChanged(nameof(IsSelectedTimelineRepair));
                this.RaisePropertyChanged(nameof(IsSelectedTimelineSetup));
                this.RaisePropertyChanged(nameof(IsSelectedTimelineInProgress));
                this.RaisePropertyChanged(nameof(IsSelectedTimelineCompleted));
            }
        }

        public bool SelectedTimelineHasData => SelectedTimelineAnnotation != null;
        public string SelectedTimelineType => SelectedTimelineAnnotation?.Type.ToString() ?? "-";
        public string SelectedTimelineResponsible => string.IsNullOrWhiteSpace(SelectedTimelineAnnotation?.Responsible) ? "-" : SelectedTimelineAnnotation!.Responsible;
        public string SelectedTimelineStart => SelectedTimelineAnnotation?.StartDate.ToString("dd.MM.yyyy HH:mm") ?? "-";
        public string SelectedTimelineEnd => SelectedTimelineAnnotation?.EndDate.ToString("dd.MM.yyyy HH:mm") ?? "-";
        public string SelectedTimelineDuration => string.IsNullOrWhiteSpace(SelectedTimelineAnnotation?.Duration) ? "-" : SelectedTimelineAnnotation!.Duration;
        public string SelectedTimelineDescription => string.IsNullOrWhiteSpace(SelectedTimelineAnnotation?.Description) ? "Выберите задачу на графике" : SelectedTimelineAnnotation!.Description;
        public string SelectedTimelineStatus => SelectedTimelineAnnotation == null ? "-" : (SelectedTimelineAnnotation.IsInProgress ? "В процессе" : "Завершена");
        public string SelectedTimelineJiraKey => string.IsNullOrWhiteSpace(SelectedTimelineAnnotation?.JiraIssueKey) ? "-" : SelectedTimelineAnnotation!.JiraIssueKey;
        public string SelectedTimelineReporter => string.IsNullOrWhiteSpace(SelectedTimelineAnnotation?.Reporter) ? "-" : SelectedTimelineAnnotation!.Reporter;
        public string SelectedTimelineComments => string.IsNullOrWhiteSpace(SelectedTimelineAnnotation?.Comments) ? "-" : SelectedTimelineAnnotation!.Comments;

        public bool IsSelectedTimelineRepair => SelectedTimelineAnnotation?.Type == Models.IssueType.Ремонт;
        public bool IsSelectedTimelineSetup => SelectedTimelineAnnotation?.Type == Models.IssueType.Настройка;
        public bool IsSelectedTimelineInProgress => SelectedTimelineAnnotation?.IsInProgress ?? false;
        public bool IsSelectedTimelineCompleted => SelectedTimelineAnnotation != null && !SelectedTimelineAnnotation.IsInProgress;

        private ReactiveCommand<DateTime, Unit>? _showDayTimelineCommand;
        public ReactiveCommand<DateTime, Unit>? ShowDayTimelineCommand
        {
            get => _showDayTimelineCommand;
            set => this.RaiseAndSetIfChanged(ref _showDayTimelineCommand, value);
        }
        public ReactiveCommand<Models.Annotation?, Unit> SelectTimelineAnnotationCommand { get; }

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

        public DateTime DowntimeAnalysisDate
        {
            get => Downtime.DowntimeAnalysisDate;
            set
            {
                Downtime.DowntimeAnalysisDate = value;
                this.RaisePropertyChanged(nameof(DowntimeAnalysisDate));
            }
        }

        public int DowntimeAffectedEquipmentCount => Downtime.DowntimeAffectedEquipmentCount;
        public int DowntimeTotalIssues => Downtime.DowntimeTotalIssues;
        public int DowntimeRepairsCount => Downtime.DowntimeRepairsCount;
        public int DowntimeSetupsCount => Downtime.DowntimeSetupsCount;
        public double DowntimeAffectedSharePercent => Downtime.DowntimeAffectedSharePercent;
        public string DowntimeTotalDuration => Downtime.DowntimeTotalDuration;
        public string DowntimeAvgIssuesPerEquipment => Downtime.DowntimeAvgIssuesPerEquipment;
        public string DowntimePeakHour => Downtime.DowntimePeakHour;
        public string DowntimeTopEquipment => Downtime.DowntimeTopEquipment;
        public int DowntimeTopEquipmentIssues => Downtime.DowntimeTopEquipmentIssues;

        private int _employeeTotalCount;
        public int EmployeeTotalCount
        {
            get => _employeeTotalCount;
            set => this.RaiseAndSetIfChanged(ref _employeeTotalCount, value);
        }

        private int _employeeTotalIssues;
        public int EmployeeTotalIssues
        {
            get => _employeeTotalIssues;
            set => this.RaiseAndSetIfChanged(ref _employeeTotalIssues, value);
        }

        private string _employeeAvgDuration = "00:00";
        public string EmployeeAvgDuration
        {
            get => _employeeAvgDuration;
            set => this.RaiseAndSetIfChanged(ref _employeeAvgDuration, value);
        }

        private double _employeeCoveragePercent;
        public double EmployeeCoveragePercent
        {
            get => _employeeCoveragePercent;
            set
            {
                var changed = Math.Abs(_employeeCoveragePercent - value) > 0.0001;
                this.RaiseAndSetIfChanged(ref _employeeCoveragePercent, value);
                if (changed)
                {
                    this.RaisePropertyChanged(nameof(EmployeeCoverageSweepAngle));
                }
            }
        }

        public double EmployeeCoverageSweepAngle => EmployeeCoveragePercent * 3.6;

        private string _employeeTopByIssues = "-";
        public string EmployeeTopByIssues
        {
            get => _employeeTopByIssues;
            set => this.RaiseAndSetIfChanged(ref _employeeTopByIssues, value);
        }

        private string _employeeTopByDuration = "-";
        public string EmployeeTopByDuration
        {
            get => _employeeTopByDuration;
            set => this.RaiseAndSetIfChanged(ref _employeeTopByDuration, value);
        }

        private string _employeeTopByIssuesValue = "0";
        public string EmployeeTopByIssuesValue
        {
            get => _employeeTopByIssuesValue;
            set => this.RaiseAndSetIfChanged(ref _employeeTopByIssuesValue, value);
        }

        private string _employeeTopByDurationValue = "00:00";
        public string EmployeeTopByDurationValue
        {
            get => _employeeTopByDurationValue;
            set => this.RaiseAndSetIfChanged(ref _employeeTopByDurationValue, value);
        }

        private int _employeeUnassignedIssues;
        public int EmployeeUnassignedIssues
        {
            get => _employeeUnassignedIssues;
            set => this.RaiseAndSetIfChanged(ref _employeeUnassignedIssues, value);
        }

        private int _employeeRepairsTotal;
        public int EmployeeRepairsTotal
        {
            get => _employeeRepairsTotal;
            set => this.RaiseAndSetIfChanged(ref _employeeRepairsTotal, value);
        }

        private int _employeeSetupsTotal;
        public int EmployeeSetupsTotal
        {
            get => _employeeSetupsTotal;
            set => this.RaiseAndSetIfChanged(ref _employeeSetupsTotal, value);
        }

        private string _employeeAvgEquipmentPerEmployee = "0.0";
        public string EmployeeAvgEquipmentPerEmployee
        {
            get => _employeeAvgEquipmentPerEmployee;
            set => this.RaiseAndSetIfChanged(ref _employeeAvgEquipmentPerEmployee, value);
        }

        private double _slaTargetMinutes = 30;
        public double SlaTargetMinutes
        {
            get => _slaTargetMinutes;
            set
            {
                var normalized = Math.Max(30, Math.Round(value));
                this.RaiseAndSetIfChanged(ref _slaTargetMinutes, normalized);
                if (_masterEquipment.Count > 0)
                    BuildEmployeeAnalysis();
            }
        }

        private double _employeeSlaCompliancePercent;
        public double EmployeeSlaCompliancePercent
        {
            get => _employeeSlaCompliancePercent;
            set
            {
                var changed = Math.Abs(_employeeSlaCompliancePercent - value) > 0.0001;
                this.RaiseAndSetIfChanged(ref _employeeSlaCompliancePercent, value);
                if (changed)
                {
                    this.RaisePropertyChanged(nameof(EmployeeSlaSweepAngle));
                    this.RaisePropertyChanged(nameof(IsEmployeeSlaDanger));
                    this.RaisePropertyChanged(nameof(IsEmployeeSlaWarning));
                    this.RaisePropertyChanged(nameof(IsEmployeeSlaSuccess));
                }
            }
        }

        public double EmployeeSlaSweepAngle => EmployeeSlaCompliancePercent * 3.6;
        public bool IsEmployeeSlaDanger => EmployeeSlaCompliancePercent < 80.0;
        public bool IsEmployeeSlaWarning => EmployeeSlaCompliancePercent >= 80.0 && EmployeeSlaCompliancePercent < 95.0;
        public bool IsEmployeeSlaSuccess => EmployeeSlaCompliancePercent >= 95.0;

        private int _employeeSlaBreaches;
        public int EmployeeSlaBreaches
        {
            get => _employeeSlaBreaches;
            set => this.RaiseAndSetIfChanged(ref _employeeSlaBreaches, value);
        }

        public int DashboardCurrentPeriodIssues
        {
            get => Dashboard.DashboardCurrentPeriodIssues;
            set
            {
                Dashboard.DashboardCurrentPeriodIssues = value;
                this.RaisePropertyChanged(nameof(DashboardCurrentPeriodIssues));
            }
        }

        public int DashboardPreviousPeriodIssues
        {
            get => Dashboard.DashboardPreviousPeriodIssues;
            set
            {
                Dashboard.DashboardPreviousPeriodIssues = value;
                this.RaisePropertyChanged(nameof(DashboardPreviousPeriodIssues));
            }
        }

        public double DashboardIssuesTrendPercent
        {
            get => Dashboard.DashboardIssuesTrendPercent;
            set
            {
                Dashboard.DashboardIssuesTrendPercent = value;
                this.RaisePropertyChanged(nameof(DashboardIssuesTrendPercent));
            }
        }

        public string DashboardIssuesTrendText
        {
            get => Dashboard.DashboardIssuesTrendText;
            set
            {
                Dashboard.DashboardIssuesTrendText = value;
                this.RaisePropertyChanged(nameof(DashboardIssuesTrendText));
            }
        }

        private int _dashboardCurrentPeriodRepairs;
        public int DashboardCurrentPeriodRepairs
        {
            get => _dashboardCurrentPeriodRepairs;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodRepairs, value);
        }

        private int _dashboardCurrentPeriodSetups;
        public int DashboardCurrentPeriodSetups
        {
            get => _dashboardCurrentPeriodSetups;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodSetups, value);
        }

        public string DashboardCurrentPeriodAvgDuration
        {
            get => Dashboard.DashboardCurrentPeriodAvgDuration;
            set
            {
                Dashboard.DashboardCurrentPeriodAvgDuration = value;
                this.RaisePropertyChanged(nameof(DashboardCurrentPeriodAvgDuration));
            }
        }

        public int DashboardCurrentPeriodAffectedEquipment
        {
            get => Dashboard.DashboardCurrentPeriodAffectedEquipment;
            set
            {
                Dashboard.DashboardCurrentPeriodAffectedEquipment = value;
                this.RaisePropertyChanged(nameof(DashboardCurrentPeriodAffectedEquipment));
            }
        }

        public int DashboardEquipmentInSystemCount
        {
            get => Dashboard.DashboardEquipmentInSystemCount;
            set
            {
                Dashboard.DashboardEquipmentInSystemCount = value;
                this.RaisePropertyChanged(nameof(DashboardEquipmentInSystemCount));
            }
        }

        public int DashboardCurrentPeriodActiveEmployees
        {
            get => Dashboard.DashboardCurrentPeriodActiveEmployees;
            set
            {
                Dashboard.DashboardCurrentPeriodActiveEmployees = value;
                this.RaisePropertyChanged(nameof(DashboardCurrentPeriodActiveEmployees));
            }
        }

        public double DashboardCurrentPeriodSlaCompliancePercent
        {
            get => Dashboard.DashboardCurrentPeriodSlaCompliancePercent;
            set
            {
                Dashboard.DashboardCurrentPeriodSlaCompliancePercent = value;
                this.RaisePropertyChanged(nameof(DashboardCurrentPeriodSlaCompliancePercent));
            }
        }

        public int DashboardCurrentPeriodSlaBreaches
        {
            get => Dashboard.DashboardCurrentPeriodSlaBreaches;
            set
            {
                Dashboard.DashboardCurrentPeriodSlaBreaches = value;
                this.RaisePropertyChanged(nameof(DashboardCurrentPeriodSlaBreaches));
            }
        }

        public int DashboardMaxIssuesInMonth
        {
            get => Dashboard.DashboardMaxIssuesInMonth;
            set
            {
                Dashboard.DashboardMaxIssuesInMonth = value;
                this.RaisePropertyChanged(nameof(DashboardMaxIssuesInMonth));
            }
        }

        public string DashboardTopPerformer
        {
            get => Dashboard.DashboardTopPerformer;
            set
            {
                Dashboard.DashboardTopPerformer = value;
                this.RaisePropertyChanged(nameof(DashboardTopPerformer));
            }
        }

        public string DashboardTopPerformerValue
        {
            get => Dashboard.DashboardTopPerformerValue;
            set
            {
                Dashboard.DashboardTopPerformerValue = value;
                this.RaisePropertyChanged(nameof(DashboardTopPerformerValue));
            }
        }

        public string DashboardRiskEquipment
        {
            get => Dashboard.DashboardRiskEquipment;
            set
            {
                Dashboard.DashboardRiskEquipment = value;
                this.RaisePropertyChanged(nameof(DashboardRiskEquipment));
            }
        }

        public string DashboardRiskEquipmentValue
        {
            get => Dashboard.DashboardRiskEquipmentValue;
            set
            {
                Dashboard.DashboardRiskEquipmentValue = value;
                this.RaisePropertyChanged(nameof(DashboardRiskEquipmentValue));
            }
        }

        public ObservableCollection<string> EmployeeAnalysisMonthOptions { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> EmployeeSubdivisionFilters { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> EmployeeTimelineEmployees { get; } = new ObservableCollection<string>();

        private string _selectedEmployeeTimelineEmployee = "Все сотрудники";
        public string SelectedEmployeeTimelineEmployee
        {
            get => _selectedEmployeeTimelineEmployee;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedEmployeeTimelineEmployee, value);
                if (_masterEquipment.Count > 0)
                    BuildEmployeeSelectedDayTimeline();
            }
        }

        private string _selectedEmployeeSubdivisionFilter = "Все группы";
        public string SelectedEmployeeSubdivisionFilter
        {
            get => _selectedEmployeeSubdivisionFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedEmployeeSubdivisionFilter, value);
                if (_masterEquipment.Count > 0)
                    BuildEmployeeAnalysis();
            }
        }

        private DateTime? _employeeTimelineDate = DateTime.Now.Date;
        public DateTime? EmployeeTimelineDate
        {
            get => _employeeTimelineDate;
            set
            {
                this.RaiseAndSetIfChanged(ref _employeeTimelineDate, value);
                if (_masterEquipment.Count > 0)
                    BuildEmployeeSelectedDayTimeline();
            }
        }

        private ObservableCollection<Models.TimelinePoint> _employeeTimelinePoints = new ObservableCollection<Models.TimelinePoint>();
        public ObservableCollection<Models.TimelinePoint> EmployeeTimelinePoints
        {
            get => _employeeTimelinePoints;
            set => this.RaiseAndSetIfChanged(ref _employeeTimelinePoints, value);
        }

        private ObservableCollection<Models.TimelinePoint> _employeeRepairsTimelinePoints = new ObservableCollection<Models.TimelinePoint>();
        public ObservableCollection<Models.TimelinePoint> EmployeeRepairsTimelinePoints
        {
            get => _employeeRepairsTimelinePoints;
            set => this.RaiseAndSetIfChanged(ref _employeeRepairsTimelinePoints, value);
        }

        private ObservableCollection<Models.TimelinePoint> _employeeSetupsTimelinePoints = new ObservableCollection<Models.TimelinePoint>();
        public ObservableCollection<Models.TimelinePoint> EmployeeSetupsTimelinePoints
        {
            get => _employeeSetupsTimelinePoints;
            set => this.RaiseAndSetIfChanged(ref _employeeSetupsTimelinePoints, value);
        }

        private ObservableCollection<Models.Annotation> _employeeTimelineAnnotations = new ObservableCollection<Models.Annotation>();
        public ObservableCollection<Models.Annotation> EmployeeTimelineAnnotations
        {
            get => _employeeTimelineAnnotations;
            set => this.RaiseAndSetIfChanged(ref _employeeTimelineAnnotations, value);
        }

        private string _selectedEmployeeAnalysisMonth = "Текущий месяц";
        public string SelectedEmployeeAnalysisMonth
        {
            get => _selectedEmployeeAnalysisMonth;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedEmployeeAnalysisMonth, value);
                if (_masterEquipment.Count > 0)
                    BuildEmployeeAnalysis();
            }
        }

        private string _employeeAnalysisPeriodDescription = "Текущий месяц";
        public string EmployeeAnalysisPeriodDescription
        {
            get => _employeeAnalysisPeriodDescription;
            set => this.RaiseAndSetIfChanged(ref _employeeAnalysisPeriodDescription, value);
        }

        private int _faultsForDay;
        public int FaultsForDay
        {
            get => _faultsForDay;
            set
            {
                this.RaiseAndSetIfChanged(ref _faultsForDay, value);
                this.RaisePropertyChanged(nameof(HasFaultsForDay));
            }
        }

        public bool HasFaultsForDay => FaultsForDay > 0;

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

        public ObservableCollection<string> DowntimeIssueTypeFilters => Downtime.DowntimeIssueTypeFilters;
        public ObservableCollection<string> DowntimeStatusFilters => Downtime.DowntimeStatusFilters;
        public ObservableCollection<string> DowntimeResponsibleFilters => Downtime.DowntimeResponsibleFilters;
        public ObservableCollection<string> DowntimeSubdivisionFilters => Downtime.DowntimeSubdivisionFilters;

        public string SelectedDowntimeIssueTypeFilter
        {
            get => Downtime.SelectedDowntimeIssueTypeFilter;
            set
            {
                Downtime.SelectedDowntimeIssueTypeFilter = value;
                this.RaisePropertyChanged(nameof(SelectedDowntimeIssueTypeFilter));
            }
        }

        public string SelectedDowntimeSubdivisionFilter
        {
            get => Downtime.SelectedDowntimeSubdivisionFilter;
            set
            {
                Downtime.SelectedDowntimeSubdivisionFilter = value;
                this.RaisePropertyChanged(nameof(SelectedDowntimeSubdivisionFilter));
            }
        }

        public string SelectedDowntimeStatusFilter
        {
            get => Downtime.SelectedDowntimeStatusFilter;
            set
            {
                Downtime.SelectedDowntimeStatusFilter = value;
                this.RaisePropertyChanged(nameof(SelectedDowntimeStatusFilter));
            }
        }

        public string SelectedDowntimeResponsibleFilter
        {
            get => Downtime.SelectedDowntimeResponsibleFilter;
            set
            {
                Downtime.SelectedDowntimeResponsibleFilter = value;
                this.RaisePropertyChanged(nameof(SelectedDowntimeResponsibleFilter));
            }
        }

        public string DowntimeEquipmentSearchQuery
        {
            get => Downtime.DowntimeEquipmentSearchQuery;
            set
            {
                Downtime.DowntimeEquipmentSearchQuery = value;
                this.RaisePropertyChanged(nameof(DowntimeEquipmentSearchQuery));
            }
        }

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

        private void FillSearchWithFirstEquipmentIfNeeded(bool force = false)
        {
            if (EquipmentCollection.Count == 0)
                return;

            if (!force && !string.IsNullOrWhiteSpace(SearchQuery))
                return;

            SearchQuery = EquipmentCollection[0].Title ?? string.Empty;
        }

        public MainWindowViewModel()
        {
            Dashboard = new DashboardViewModel(this);
            Downtime = new DowntimeViewModel(this);
            Reports = new ReportsViewModel(this);
            Settings = new SettingsViewModel(this);

            HeatmapSettingOptions.Add(FailureHeatmapOption);
            HeatmapSettingOptions.Add(DowntimeHeatmapOption);
            SelectedHeatmapSetting = FailureHeatmapOption;
            SelectTimelineAnnotationCommand = ReactiveCommand.Create<Models.Annotation?>(annotation =>
            {
                SelectedTimelineAnnotation = annotation;
            });

            EmployeeTimelineEmployees.Add("Все сотрудники");
            SelectedEmployeeTimelineEmployee = "Все сотрудники";
            RebuildDowntimeResponsibleFilters();
            RebuildDowntimeSubdivisionFilters();

            // prepare day headers 1..31 for the heatmap top row
            for (int i = 1; i <= 31; i++)
            {
                DayHeaders.Add(i);
            }
            // prepare hours 0..23 for timeline labels
            for (int h = 0; h < 24; h++)
                DayHours.Add(h);
            // initialize collection before applying filters (prevents null refs)
            EquipmentCollection = new ObservableCollection<EquipmentInfo>();

            IsLoading = true;
            System.Threading.Tasks.Task.Run(() =>
            {
                var xmlDataDecoder = new XmlDataDecoder();
                var all = xmlDataDecoder.DecodeEquipment().ToList();
                return all;
            }).ContinueWith(t =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        var all = t.Result;
                        _masterEquipment = all;
                        RebuildDowntimeResponsibleFilters();
                        RebuildDowntimeSubdivisionFilters();
                        RebuildDashboardFilters();
                        RebuildEmployeeSubdivisionFilters();
                        RebuildEmployeeMonthOptions();
                        ApplyTypeFilterAndSort();
                        FillSearchWithFirstEquipmentIfNeeded();
                    }
                    catch
                    {
                        // ignore default load failure
                    }
                    finally
                    {
                        IsLoading = false;
                    }
                });
            });

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
                DowntimePercent = downPercent.ToString("0.0") + "%"; // label for 'Простой'
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
                    DowntimePercent = downPercent.ToString("0.0") + "%";
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
                    SelectedTimelineAnnotation = null;
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
                        var actualDuration = issue.End - issue.Start;
                        if (actualDuration < TimeSpan.Zero)
                            actualDuration = TimeSpan.Zero;
                        var actHours = (int)actualDuration.TotalHours;
                        var formattedDuration = $"{actHours:00}:{actualDuration.Minutes:00}";

                        var desc = issue.Description ?? string.Empty;
                        var resp = string.IsNullOrEmpty(issue.Responsible) ? "-" : issue.Responsible;
                        dayAnnotations.Add(new Models.Annotation
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

                    SelectedTimelineAnnotation = Annotations.FirstOrDefault();

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

                // build month rows dynamically based on the actual issue date range
                MonthRows.Clear();
                DateTime minDate = DateTime.Today.AddMonths(-3);
                DateTime maxDate = DateTime.Today;

                if (filteredIssues != null && filteredIssues.Count > 0)
                {
                    var earliest = filteredIssues.Min(i => i.Start);
                    var latest = filteredIssues.Max(i => i.Start);
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
                    var monthRow = new Models.MonthRow
                    {
                        Month = monthDate.Month,
                        Year = monthDate.Year,
                        MonthName = name
                    };

                    int dowOffset = ((int)monthDate.DayOfWeek + 6) % 7; // Monday-based index (0-6)
                    for (int i = 0; i < dowOffset; i++)
                    {
                        monthRow.Days.Add(new Models.DayCell { DayNumber = 0, Index = 0, IsValid = false });
                    }

                    for (int d = 1; d <= daysInMonth; d++)
                    {
                        var cell = new Models.DayCell { DayNumber = d, Index = 0, IsValid = true };
                        cell.Date = new DateTime(monthDate.Year, monthDate.Month, d);
                        var entry = DailyDowntimeIndexCollection.FirstOrDefault(x => x.Day.Date == cell.Date.Date);
                        if (entry != null)
                            cell.Index = entry.Index;
                        monthRow.Days.Add(cell);
                    }

                    while (monthRow.Days.Count < 42)
                    {
                        monthRow.Days.Add(new Models.DayCell { DayNumber = 0, Index = 0, IsValid = false });
                    }

                    MonthRows.Add(monthRow);
                    current = current.AddMonths(1);
                }
                this.RaisePropertyChanged(nameof(HeatmapYearLabel));

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

            BuildDowntimeHeatmap();
            BuildDowntimeDayEquipmentRows(DateTime.Now.Date);
            BuildEmployeeAnalysis();
        }

    }
}
