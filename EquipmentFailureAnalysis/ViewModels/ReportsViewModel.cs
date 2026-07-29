using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.Services;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace EquipmentFailureAnalysis.ViewModels
{
    public sealed class ReportsViewModel : ViewModelBase
    {
        private static readonly HashSet<string> DashboardObservedPropertyNames = new(StringComparer.Ordinal)
        {
            nameof(DashboardCurrentPeriodActiveEmployees),
            nameof(DashboardCurrentPeriodAvgDuration),
            nameof(DashboardRiskEquipment),
            nameof(DashboardRiskEquipmentValue),
            nameof(DashboardTopPerformer),
            nameof(DashboardTopPerformerValue)
        };

        private readonly MainWindowViewModel _shell;

        private DateTime? _reportStartDate = DateTime.Now.Date;
        private DateTime? _reportEndDate = DateTime.Now.Date;
        private int _reportGroupByIndex;
        private bool _reportIncludeDashboard = true;
        private bool _reportIncludeDowntime = true;
        private bool _reportIncludeEmployee = true;
        private bool _reportOpenAfterGenerate = true;
        private bool _reportOnlyInProgress;
        private bool _reportFilterByDuration;
        private decimal _reportMinDurationMinutes = 60m;
        private bool _reportFieldStart = true;
        private bool _reportFieldEnd = true;
        private bool _reportFieldEquipment = true;
        private bool _reportFieldSubdivision = true;
        private bool _reportFieldType = true;
        private bool _reportFieldResponsible = true;
        private bool _reportFieldDescription = true;
        private string _reportLastFilePath = string.Empty;
        private int _downtimeLossGranularityIndex;

        public ReportsViewModel(MainWindowViewModel shell)
        {
            _shell = shell;
            _shell.Dashboard.PropertyChanged += OnDashboardPropertyChanged;
        }

        public DateTime? ReportStartDate
        {
            get => _reportStartDate;
            set => this.RaiseAndSetIfChanged(ref _reportStartDate, value?.Date);
        }

        public DateTime? ReportEndDate
        {
            get => _reportEndDate;
            set => this.RaiseAndSetIfChanged(ref _reportEndDate, value?.Date);
        }

        public void SetPeriodPreset(string presetKey)
        {
            var now = DateTime.Now;
            var currentYear = now.Year;

            switch (presetKey)
            {
                case "1year":
                    ReportStartDate = new DateTime(currentYear, 1, 1);
                    ReportEndDate = new DateTime(currentYear, 12, 31);
                    break;
                case "2years":
                    ReportStartDate = new DateTime(currentYear - 1, 1, 1);
                    ReportEndDate = new DateTime(currentYear, 12, 31);
                    break;
                case "3years":
                    ReportStartDate = new DateTime(currentYear - 2, 1, 1);
                    ReportEndDate = new DateTime(currentYear, 12, 31);
                    break;
                case "5years":
                    ReportStartDate = new DateTime(currentYear - 4, 1, 1);
                    ReportEndDate = new DateTime(currentYear, 12, 31);
                    break;
                case "all":
                    var allEquipment = _shell.GetEquipmentForReports();
                    var allIssues = allEquipment.SelectMany(e => e.Issues ?? Enumerable.Empty<Issue>()).ToList();
                    if (allIssues.Count > 0)
                    {
                        var minStart = allIssues.Min(i => i.Start);
                        var maxEnd = allIssues.Max(i => i.IsInProgress ? now : i.End);
                        ReportStartDate = minStart.Date;
                        ReportEndDate = maxEnd.Date;
                    }
                    else
                    {
                        ReportStartDate = new DateTime(currentYear, 1, 1);
                        ReportEndDate = now.Date;
                    }
                    break;
            }
        }

        public int ReportGroupByIndex
        {
            get => _reportGroupByIndex;
            set
            {
                this.RaiseAndSetIfChanged(ref _reportGroupByIndex, Math.Clamp(value, 0, 3));
                this.RaisePropertyChanged(nameof(ReportGroupByRussian));
            }
        }

        public string ReportGroupByKey
        {
            get => ReportGroupByIndex switch
            {
                1 => "month",
                2 => "employee",
                3 => "equipment",
                _ => "day"
            };
            set
            {
                var normalized = NormalizeGroupByKey(value);
                ReportGroupByIndex = normalized switch
                {
                    "month" => 1,
                    "employee" => 2,
                    "equipment" => 3,
                    _ => 0
                };
            }
        }

        public bool ReportIncludeDashboard
        {
            get => _reportIncludeDashboard;
            set
            {
                this.RaiseAndSetIfChanged(ref _reportIncludeDashboard, value);
                this.RaisePropertyChanged(nameof(ActiveModulesSummary));
            }
        }

        public bool ReportIncludeDowntime
        {
            get => _reportIncludeDowntime;
            set
            {
                this.RaiseAndSetIfChanged(ref _reportIncludeDowntime, value);
                this.RaisePropertyChanged(nameof(ActiveModulesSummary));
            }
        }

        public bool ReportIncludeEmployee
        {
            get => _reportIncludeEmployee;
            set
            {
                this.RaiseAndSetIfChanged(ref _reportIncludeEmployee, value);
                this.RaisePropertyChanged(nameof(ActiveModulesSummary));
            }
        }

        public bool ReportOpenAfterGenerate
        {
            get => _reportOpenAfterGenerate;
            set => this.RaiseAndSetIfChanged(ref _reportOpenAfterGenerate, value);
        }

        public bool ReportOnlyInProgress
        {
            get => _reportOnlyInProgress;
            set => this.RaiseAndSetIfChanged(ref _reportOnlyInProgress, value);
        }

        public bool ReportFilterByDuration
        {
            get => _reportFilterByDuration;
            set => this.RaiseAndSetIfChanged(ref _reportFilterByDuration, value);
        }

        public decimal ReportMinDurationMinutes
        {
            get => _reportMinDurationMinutes;
            set => this.RaiseAndSetIfChanged(ref _reportMinDurationMinutes, Math.Max(0, value));
        }

        public bool ReportFieldStart
        {
            get => _reportFieldStart;
            set
            {
                this.RaiseAndSetIfChanged(ref _reportFieldStart, value);
                this.RaisePropertyChanged(nameof(ActiveColumnsCountSummary));
            }
        }

        public bool ReportFieldEnd
        {
            get => _reportFieldEnd;
            set
            {
                this.RaiseAndSetIfChanged(ref _reportFieldEnd, value);
                this.RaisePropertyChanged(nameof(ActiveColumnsCountSummary));
            }
        }

        public bool ReportFieldEquipment
        {
            get => _reportFieldEquipment;
            set
            {
                this.RaiseAndSetIfChanged(ref _reportFieldEquipment, value);
                this.RaisePropertyChanged(nameof(ActiveColumnsCountSummary));
            }
        }

        public bool ReportFieldSubdivision
        {
            get => _reportFieldSubdivision;
            set
            {
                this.RaiseAndSetIfChanged(ref _reportFieldSubdivision, value);
                this.RaisePropertyChanged(nameof(ActiveColumnsCountSummary));
            }
        }

        public bool ReportFieldType
        {
            get => _reportFieldType;
            set
            {
                this.RaiseAndSetIfChanged(ref _reportFieldType, value);
                this.RaisePropertyChanged(nameof(ActiveColumnsCountSummary));
            }
        }

        public bool ReportFieldResponsible
        {
            get => _reportFieldResponsible;
            set
            {
                this.RaiseAndSetIfChanged(ref _reportFieldResponsible, value);
                this.RaisePropertyChanged(nameof(ActiveColumnsCountSummary));
            }
        }

        public bool ReportFieldDescription
        {
            get => _reportFieldDescription;
            set
            {
                this.RaiseAndSetIfChanged(ref _reportFieldDescription, value);
                this.RaisePropertyChanged(nameof(ActiveColumnsCountSummary));
            }
        }

        public string ReportLastFilePath
        {
            get => _reportLastFilePath;
            set => this.RaiseAndSetIfChanged(ref _reportLastFilePath, value ?? string.Empty);
        }

        public int DashboardCurrentPeriodActiveEmployees => _shell.Dashboard.DashboardCurrentPeriodActiveEmployees;
        public string DashboardCurrentPeriodAvgDuration => _shell.Dashboard.DashboardCurrentPeriodAvgDuration;
        public string DashboardRiskEquipment => _shell.Dashboard.DashboardRiskEquipment;
        public string DashboardRiskEquipmentValue => _shell.Dashboard.DashboardRiskEquipmentValue;
        public string DashboardTopPerformer => _shell.Dashboard.DashboardTopPerformer;
        public string DashboardTopPerformerValue => _shell.Dashboard.DashboardTopPerformerValue;

        private void OnDashboardPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.PropertyName))
                return;

            if (DashboardObservedPropertyNames.Contains(e.PropertyName))
                this.RaisePropertyChanged(e.PropertyName);
        }

        private static string NormalizeGroupByKey(string? value)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
                return "day";

            if (string.Equals(normalized, "day", StringComparison.OrdinalIgnoreCase))
                return "day";
            if (string.Equals(normalized, "month", StringComparison.OrdinalIgnoreCase))
                return "month";
            if (string.Equals(normalized, "employee", StringComparison.OrdinalIgnoreCase))
                return "employee";
            if (string.Equals(normalized, "equipment", StringComparison.OrdinalIgnoreCase))
                return "equipment";

            if (normalized.Contains("меся", StringComparison.CurrentCultureIgnoreCase))
                return "month";
            if (normalized.Contains("сотруд", StringComparison.CurrentCultureIgnoreCase))
                return "employee";
            if (normalized.Contains("оборуд", StringComparison.CurrentCultureIgnoreCase))
                return "equipment";

            return "day";
        }

        public string ReportGroupByRussian => ReportGroupByIndex switch
        {
            1 => "По месяцам",
            2 => "По сотрудникам",
            3 => "По оборудованию",
            _ => "По дням"
        };

        public string ActiveModulesSummary
        {
            get
            {
                var list = new List<string>();
                if (ReportIncludeDashboard) list.Add("Панель");
                if (ReportIncludeDowntime) list.Add("Простои");
                if (ReportIncludeEmployee) list.Add("Сотрудники");
                return list.Count == 0 ? "Нет выбранных разделов" : string.Join(", ", list);
            }
        }

        public string ActiveColumnsCountSummary
        {
            get
            {
                int total = 7;
                int selected = 0;
                if (ReportFieldStart) selected++;
                if (ReportFieldEnd) selected++;
                if (ReportFieldEquipment) selected++;
                if (ReportFieldSubdivision) selected++;
                if (ReportFieldType) selected++;
                if (ReportFieldResponsible) selected++;
                if (ReportFieldDescription) selected++;
                return $"{selected} из {total}";
            }
        }

        public bool TryBuildHtmlReportOptions(DateTime today, out HtmlReportOptions options, out string? validationError)
        {
            var startDate = ReportStartDate?.Date ?? today.Date;
            var endDate = ReportEndDate?.Date ?? today.Date;

            options = new HtmlReportOptions
            {
                StartDate = startDate,
                EndDate = endDate,
                GroupBy = ReportGroupByKey,
                IncludeDashboard = ReportIncludeDashboard,
                IncludeDowntime = ReportIncludeDowntime,
                IncludeEmployee = ReportIncludeEmployee,
                OnlyInProgress = ReportOnlyInProgress,
                FilterByDuration = ReportFilterByDuration,
                MinDurationMinutes = (double)ReportMinDurationMinutes,
                ShowStart = ReportFieldStart,
                ShowEnd = ReportFieldEnd,
                ShowEquipment = ReportFieldEquipment,
                ShowSubdivision = ReportFieldSubdivision,
                ShowType = ReportFieldType,
                ShowResponsible = ReportFieldResponsible,
                ShowDescription = ReportFieldDescription
            };

            if (options.EndDate < options.StartDate)
            {
                validationError = "Дата окончания периода не может быть меньше даты начала.";
                return false;
            }

            validationError = null;
            return true;
        }

        public int DowntimeLossGranularityIndex
        {
            get => _downtimeLossGranularityIndex;
            set => this.RaiseAndSetIfChanged(ref _downtimeLossGranularityIndex, Math.Clamp(value, 0, 1));
        }

        public PeriodGranularity DowntimeLossGranularity => DowntimeLossGranularityIndex == 1 ? PeriodGranularity.Quarterly : PeriodGranularity.Monthly;

        public bool TryBuildDowntimeLossReport(out EquipmentDowntimeLossReport? report, out string? validationError)
        {
            var today = DateTime.Now;
            var startDate = ReportStartDate?.Date ?? today.Date;
            var endDate = ReportEndDate?.Date ?? today.Date;

            if (endDate < startDate)
            {
                report = null;
                validationError = "Дата окончания периода не может быть меньше даты начала.";
                return false;
            }

            var service = new EquipmentDowntimeLossService();
            report = service.BuildReport(_shell.GetEquipmentForReports(), startDate, endDate, DowntimeLossGranularity);
            validationError = null;
            return true;
        }

        public void EnsureDefaultPeriod(DateTime today)
        {
            var date = today.Date;
            if (!ReportStartDate.HasValue)
                ReportStartDate = date;
            if (!ReportEndDate.HasValue)
                ReportEndDate = date;
        }
    }
}