using EquipmentFailureAnalysis.Services;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;

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

        public int ReportGroupByIndex
        {
            get => _reportGroupByIndex;
            set => this.RaiseAndSetIfChanged(ref _reportGroupByIndex, Math.Clamp(value, 0, 3));
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
            set => this.RaiseAndSetIfChanged(ref _reportIncludeDashboard, value);
        }

        public bool ReportIncludeDowntime
        {
            get => _reportIncludeDowntime;
            set => this.RaiseAndSetIfChanged(ref _reportIncludeDowntime, value);
        }

        public bool ReportIncludeEmployee
        {
            get => _reportIncludeEmployee;
            set => this.RaiseAndSetIfChanged(ref _reportIncludeEmployee, value);
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
            set => this.RaiseAndSetIfChanged(ref _reportFieldStart, value);
        }

        public bool ReportFieldEnd
        {
            get => _reportFieldEnd;
            set => this.RaiseAndSetIfChanged(ref _reportFieldEnd, value);
        }

        public bool ReportFieldEquipment
        {
            get => _reportFieldEquipment;
            set => this.RaiseAndSetIfChanged(ref _reportFieldEquipment, value);
        }

        public bool ReportFieldSubdivision
        {
            get => _reportFieldSubdivision;
            set => this.RaiseAndSetIfChanged(ref _reportFieldSubdivision, value);
        }

        public bool ReportFieldType
        {
            get => _reportFieldType;
            set => this.RaiseAndSetIfChanged(ref _reportFieldType, value);
        }

        public bool ReportFieldResponsible
        {
            get => _reportFieldResponsible;
            set => this.RaiseAndSetIfChanged(ref _reportFieldResponsible, value);
        }

        public bool ReportFieldDescription
        {
            get => _reportFieldDescription;
            set => this.RaiseAndSetIfChanged(ref _reportFieldDescription, value);
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
