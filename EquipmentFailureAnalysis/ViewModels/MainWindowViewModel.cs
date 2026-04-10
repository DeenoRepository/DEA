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
    public class MainWindowViewModel : ViewModelBase
    {
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
        public ObservableCollection<DailyDowntimeIndex> DailyDowntimeIndexCollection { get; set; }
        public ObservableCollection<Models.MonthRow> MonthRows { get; set; } = new ObservableCollection<Models.MonthRow>();
        public ObservableCollection<Models.MonthRow> DowntimeMonthRows { get; set; } = new ObservableCollection<Models.MonthRow>();
        public ObservableCollection<Models.DowntimeEquipmentRow> DowntimeDayEquipmentRows { get; set; } = new ObservableCollection<Models.DowntimeEquipmentRow>();
        private ObservableCollection<Models.EmployeeAnalysisRow> _employeeAnalysisRows = new ObservableCollection<Models.EmployeeAnalysisRow>();
        public ObservableCollection<Models.EmployeeAnalysisRow> EmployeeAnalysisRows
        {
            get => _employeeAnalysisRows;
            set => this.RaiseAndSetIfChanged(ref _employeeAnalysisRows, value);
        }

        private ObservableCollection<Models.DashboardTrendPoint> _dashboardMonthlyTrends = new ObservableCollection<Models.DashboardTrendPoint>();
        public ObservableCollection<Models.DashboardTrendPoint> DashboardMonthlyTrends
        {
            get => _dashboardMonthlyTrends;
            set => this.RaiseAndSetIfChanged(ref _dashboardMonthlyTrends, value);
        }
        public ReactiveCommand<EquipmentInfo, Unit> LoadEquipmentCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetUniversalFiltersCommand { get; }
        public ObservableCollection<int> DayHeaders { get; set; } = new ObservableCollection<int>();
        public ObservableCollection<int> DayHours { get; set; } = new ObservableCollection<int>();
        public ReactiveCommand<DateTime, Unit> ShowDowntimeDayCommand { get; }
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

        private DateTime _downtimeAnalysisDate = DateTime.Now.Date;
        public DateTime DowntimeAnalysisDate
        {
            get => _downtimeAnalysisDate;
            set => this.RaiseAndSetIfChanged(ref _downtimeAnalysisDate, value);
        }

        private int _downtimeAffectedEquipmentCount;
        public int DowntimeAffectedEquipmentCount
        {
            get => _downtimeAffectedEquipmentCount;
            set => this.RaiseAndSetIfChanged(ref _downtimeAffectedEquipmentCount, value);
        }

        private int _downtimeTotalIssues;
        public int DowntimeTotalIssues
        {
            get => _downtimeTotalIssues;
            set => this.RaiseAndSetIfChanged(ref _downtimeTotalIssues, value);
        }

        private int _downtimeRepairsCount;
        public int DowntimeRepairsCount
        {
            get => _downtimeRepairsCount;
            set => this.RaiseAndSetIfChanged(ref _downtimeRepairsCount, value);
        }

        private int _downtimeSetupsCount;
        public int DowntimeSetupsCount
        {
            get => _downtimeSetupsCount;
            set => this.RaiseAndSetIfChanged(ref _downtimeSetupsCount, value);
        }

        private double _downtimeAffectedSharePercent;
        public double DowntimeAffectedSharePercent
        {
            get => _downtimeAffectedSharePercent;
            set => this.RaiseAndSetIfChanged(ref _downtimeAffectedSharePercent, value);
        }

        private string _downtimeTotalDuration = "00:00";
        public string DowntimeTotalDuration
        {
            get => _downtimeTotalDuration;
            set => this.RaiseAndSetIfChanged(ref _downtimeTotalDuration, value);
        }

        private string _downtimeAvgIssuesPerEquipment = "0.0";
        public string DowntimeAvgIssuesPerEquipment
        {
            get => _downtimeAvgIssuesPerEquipment;
            set => this.RaiseAndSetIfChanged(ref _downtimeAvgIssuesPerEquipment, value);
        }

        private string _downtimePeakHour = "-";
        public string DowntimePeakHour
        {
            get => _downtimePeakHour;
            set => this.RaiseAndSetIfChanged(ref _downtimePeakHour, value);
        }

        private string _downtimeTopEquipment = "-";
        public string DowntimeTopEquipment
        {
            get => _downtimeTopEquipment;
            set => this.RaiseAndSetIfChanged(ref _downtimeTopEquipment, value);
        }

        private int _downtimeTopEquipmentIssues;
        public int DowntimeTopEquipmentIssues
        {
            get => _downtimeTopEquipmentIssues;
            set => this.RaiseAndSetIfChanged(ref _downtimeTopEquipmentIssues, value);
        }

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
            set => this.RaiseAndSetIfChanged(ref _employeeCoveragePercent, value);
        }

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
            set => this.RaiseAndSetIfChanged(ref _employeeSlaCompliancePercent, value);
        }

        private int _employeeSlaBreaches;
        public int EmployeeSlaBreaches
        {
            get => _employeeSlaBreaches;
            set => this.RaiseAndSetIfChanged(ref _employeeSlaBreaches, value);
        }

        private int _dashboardCurrentPeriodIssues;
        public int DashboardCurrentPeriodIssues
        {
            get => _dashboardCurrentPeriodIssues;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodIssues, value);
        }

        private int _dashboardPreviousPeriodIssues;
        public int DashboardPreviousPeriodIssues
        {
            get => _dashboardPreviousPeriodIssues;
            set => this.RaiseAndSetIfChanged(ref _dashboardPreviousPeriodIssues, value);
        }

        private double _dashboardIssuesTrendPercent;
        public double DashboardIssuesTrendPercent
        {
            get => _dashboardIssuesTrendPercent;
            set => this.RaiseAndSetIfChanged(ref _dashboardIssuesTrendPercent, value);
        }

        private string _dashboardIssuesTrendText = "Стабильно";
        public string DashboardIssuesTrendText
        {
            get => _dashboardIssuesTrendText;
            set => this.RaiseAndSetIfChanged(ref _dashboardIssuesTrendText, value);
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

        private string _dashboardCurrentPeriodAvgDuration = "00:00";
        public string DashboardCurrentPeriodAvgDuration
        {
            get => _dashboardCurrentPeriodAvgDuration;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodAvgDuration, value);
        }

        private int _dashboardCurrentPeriodAffectedEquipment;
        public int DashboardCurrentPeriodAffectedEquipment
        {
            get => _dashboardCurrentPeriodAffectedEquipment;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodAffectedEquipment, value);
        }

        private int _dashboardCurrentPeriodActiveEmployees;
        public int DashboardCurrentPeriodActiveEmployees
        {
            get => _dashboardCurrentPeriodActiveEmployees;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodActiveEmployees, value);
        }

        private double _dashboardCurrentPeriodSlaCompliancePercent;
        public double DashboardCurrentPeriodSlaCompliancePercent
        {
            get => _dashboardCurrentPeriodSlaCompliancePercent;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodSlaCompliancePercent, value);
        }

        private int _dashboardCurrentPeriodSlaBreaches;
        public int DashboardCurrentPeriodSlaBreaches
        {
            get => _dashboardCurrentPeriodSlaBreaches;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodSlaBreaches, value);
        }

        private int _dashboardMaxIssuesInMonth;
        public int DashboardMaxIssuesInMonth
        {
            get => _dashboardMaxIssuesInMonth;
            set => this.RaiseAndSetIfChanged(ref _dashboardMaxIssuesInMonth, value);
        }

        private string _dashboardTopPerformer = "-";
        public string DashboardTopPerformer
        {
            get => _dashboardTopPerformer;
            set => this.RaiseAndSetIfChanged(ref _dashboardTopPerformer, value);
        }

        private string _dashboardTopPerformerValue = "-";
        public string DashboardTopPerformerValue
        {
            get => _dashboardTopPerformerValue;
            set => this.RaiseAndSetIfChanged(ref _dashboardTopPerformerValue, value);
        }

        private string _dashboardRiskEquipment = "-";
        public string DashboardRiskEquipment
        {
            get => _dashboardRiskEquipment;
            set => this.RaiseAndSetIfChanged(ref _dashboardRiskEquipment, value);
        }

        private string _dashboardRiskEquipmentValue = "0 событий";
        public string DashboardRiskEquipmentValue
        {
            get => _dashboardRiskEquipmentValue;
            set => this.RaiseAndSetIfChanged(ref _dashboardRiskEquipmentValue, value);
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

        public ObservableCollection<string> DowntimeIssueTypeFilters { get; } = new ObservableCollection<string>
        {
            "Все типы",
            "Ремонты",
            "Настройки"
        };

        public ObservableCollection<string> DowntimeResponsibleFilters { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> DowntimeSubdivisionFilters { get; } = new ObservableCollection<string>();

        private string _selectedDowntimeIssueTypeFilter = "Все типы";
        public string SelectedDowntimeIssueTypeFilter
        {
            get => _selectedDowntimeIssueTypeFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedDowntimeIssueTypeFilter, value);
                RefreshDowntimeAnalysis();
                RefreshFailureAnalysis();
            }
        }

        private string _selectedDowntimeSubdivisionFilter = "Все группы";
        public string SelectedDowntimeSubdivisionFilter
        {
            get => _selectedDowntimeSubdivisionFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedDowntimeSubdivisionFilter, value);
                RefreshDowntimeAnalysis();
                RefreshFailureAnalysis();
            }
        }

        private string _selectedDowntimeResponsibleFilter = "Все ответственные";
        public string SelectedDowntimeResponsibleFilter
        {
            get => _selectedDowntimeResponsibleFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedDowntimeResponsibleFilter, value);
                RefreshDowntimeAnalysis();
                RefreshFailureAnalysis();
            }
        }

        private string _downtimeEquipmentSearchQuery = string.Empty;
        public string DowntimeEquipmentSearchQuery
        {
            get => _downtimeEquipmentSearchQuery;
            set
            {
                this.RaiseAndSetIfChanged(ref _downtimeEquipmentSearchQuery, value);
                RefreshDowntimeAnalysis();
                RefreshFailureAnalysis();
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
            HeatmapSettingOptions.Add(FailureHeatmapOption);
            HeatmapSettingOptions.Add(DowntimeHeatmapOption);
            SelectedHeatmapSetting = FailureHeatmapOption;

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
            XmlDataDecoder xmlDataDecoder = new XmlDataDecoder();
            // load all equipment and set master list
            var all = xmlDataDecoder.DecodeEquipment().ToList();
            all.ForEach(e => { /* ensure Issues collection is not null */ });
            _masterEquipment = all;
            RebuildDowntimeResponsibleFilters();
            RebuildDowntimeSubdivisionFilters();
            RebuildEmployeeSubdivisionFilters();
            RebuildEmployeeMonthOptions();
            // initialize collection before applying filters (prevents null refs)
            EquipmentCollection = new ObservableCollection<EquipmentInfo>();
            // apply initial type filter and sort
            ApplyTypeFilterAndSort();
            FillSearchWithFirstEquipmentIfNeeded();

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

            ShowDowntimeDayCommand = ReactiveCommand.Create<DateTime>(date =>
            {
                BuildDowntimeDayEquipmentRows(date);
            });

            ResetUniversalFiltersCommand = ReactiveCommand.Create(ResetUniversalFilters);

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
                        dayAnnotations.Add(new Models.Annotation
                        {
                            Hour = sMin / 60.0,
                            StartHour = sMin / 60.0,
                            EndHour = eMin / 60.0,
                            Description = desc,
                            Responsible = resp,
                            StartDate = overlapStart,
                            EndDate = overlapEnd,
                            Duration = duration.ToString(@"hh\:mm"),
                            Type = issue.Type
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

            BuildDowntimeHeatmap();
            BuildDowntimeDayEquipmentRows(DateTime.Now.Date);
            BuildEmployeeAnalysis();
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
                    Type = issue.Type
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

            DashboardCurrentPeriodAffectedEquipment = currentIssues
                .Select(i => i.Equipment.Title)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Count();

            var assignedCurrentIssues = currentIssues.Where(i => !IsUnassignedResponsible(i.Issue.Responsible)).ToList();
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
                    Type = issue.Type
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
                        Type = issue.Type
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
