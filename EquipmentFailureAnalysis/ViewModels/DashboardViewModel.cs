using EquipmentFailureAnalysis.Models;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;

namespace EquipmentFailureAnalysis.ViewModels
{
    public sealed class DashboardViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel _shell;

        private int _dashboardCurrentPeriodIssues;
        private string _dashboardIssuesTrendText = "Стабильно";
        private int _dashboardPreviousPeriodIssues;
        private double _dashboardIssuesTrendPercent;
        private int _dashboardCurrentPeriodAffectedEquipment;
        private int _dashboardEquipmentInSystemCount;
        private string _dashboardCurrentPeriodAvgDuration = "00:00";
        private int _dashboardMaxIssuesInMonth;
        private double _dashboardCurrentPeriodSlaCompliancePercent;
        private int _dashboardCurrentPeriodActiveEmployees;
        private int _dashboardCurrentPeriodSlaBreaches;
        private string _dashboardTopPerformer = "-";
        private string _dashboardTopPerformerValue = "-";
        private string _dashboardRiskEquipment = "-";
        private string _dashboardRiskEquipmentValue = "0 событий";
        private string _dashboardCurrentPeriodMttr = "00:00";
        private string _dashboardCurrentPeriodMtbf = "00:00";
        private double _dashboardCurrentPeriodUnassignedSharePercent;
        private string _dashboardRecurringFailuresValue = "0 ед. / 0 событий";
        private ObservableCollection<SubdivisionRatingRow> _dashboardSubdivisionRatings = new();
        private ObservableCollection<DashboardTrendPoint> _dashboardMonthlyTrends = new();
        private ObservableCollection<ParetoPoint> _dashboardParetoPoints = new();
        private ObservableCollection<DataWarning> _dashboardDataWarnings = new();

        private bool _isRefreshingFilters;
        private string _selectedDashboardIssueTypeFilter = "Все типы";
        private string _selectedDashboardResponsibleFilter = "Все ответственные";
        private string _selectedDashboardSubdivisionFilter = "Все группы";

        public DashboardViewModel(MainWindowViewModel shell)
        {
            _shell = shell;
            DashboardIssueTypeFilters.Add("Все типы");
            DashboardIssueTypeFilters.Add("Ремонты");
            DashboardIssueTypeFilters.Add("Настройки");
            ResetDashboardFiltersCommand = ReactiveCommand.Create(ResetDashboardFilters);
        }

        public ObservableCollection<EquipmentInfo> EquipmentCollection => _shell.EquipmentCollection;
        public ObservableCollection<string> DashboardIssueTypeFilters { get; } = new();
        public ObservableCollection<string> DashboardResponsibleFilters { get; } = new();
        public ObservableCollection<string> DashboardSubdivisionFilters { get; } = new();
        public ReactiveCommand<Unit, Unit> ResetDashboardFiltersCommand { get; }

        public int DashboardCurrentPeriodIssues
        {
            get => _dashboardCurrentPeriodIssues;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodIssues, value);
        }

        public string DashboardIssuesTrendText
        {
            get => _dashboardIssuesTrendText;
            set => this.RaiseAndSetIfChanged(ref _dashboardIssuesTrendText, value);
        }

        public int DashboardPreviousPeriodIssues
        {
            get => _dashboardPreviousPeriodIssues;
            set => this.RaiseAndSetIfChanged(ref _dashboardPreviousPeriodIssues, value);
        }

        public double DashboardIssuesTrendPercent
        {
            get => _dashboardIssuesTrendPercent;
            set => this.RaiseAndSetIfChanged(ref _dashboardIssuesTrendPercent, value);
        }

        public int DashboardCurrentPeriodAffectedEquipment
        {
            get => _dashboardCurrentPeriodAffectedEquipment;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodAffectedEquipment, value);
        }

        public int DashboardEquipmentInSystemCount
        {
            get => _dashboardEquipmentInSystemCount;
            set => this.RaiseAndSetIfChanged(ref _dashboardEquipmentInSystemCount, value);
        }

        public string DashboardCurrentPeriodAvgDuration
        {
            get => _dashboardCurrentPeriodAvgDuration;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodAvgDuration, value);
        }

        public int DashboardMaxIssuesInMonth
        {
            get => _dashboardMaxIssuesInMonth;
            set => this.RaiseAndSetIfChanged(ref _dashboardMaxIssuesInMonth, value);
        }

        public double DashboardCurrentPeriodSlaCompliancePercent
        {
            get => _dashboardCurrentPeriodSlaCompliancePercent;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodSlaCompliancePercent, value);
        }

        public ObservableCollection<DashboardTrendPoint> DashboardMonthlyTrends
        {
            get => _dashboardMonthlyTrends;
            set => this.RaiseAndSetIfChanged(ref _dashboardMonthlyTrends, value);
        }

        public ObservableCollection<ParetoPoint> DashboardParetoPoints
        {
            get => _dashboardParetoPoints;
            set => this.RaiseAndSetIfChanged(ref _dashboardParetoPoints, value);
        }

        public ObservableCollection<DataWarning> DashboardDataWarnings
        {
            get => _dashboardDataWarnings;
            set => this.RaiseAndSetIfChanged(ref _dashboardDataWarnings, value);
        }

        public int DashboardCurrentPeriodActiveEmployees
        {
            get => _dashboardCurrentPeriodActiveEmployees;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodActiveEmployees, value);
        }

        public int DashboardCurrentPeriodSlaBreaches
        {
            get => _dashboardCurrentPeriodSlaBreaches;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodSlaBreaches, value);
        }

        public string DashboardTopPerformer
        {
            get => _dashboardTopPerformer;
            set => this.RaiseAndSetIfChanged(ref _dashboardTopPerformer, value);
        }

        public string DashboardTopPerformerValue
        {
            get => _dashboardTopPerformerValue;
            set => this.RaiseAndSetIfChanged(ref _dashboardTopPerformerValue, value);
        }

        public string DashboardRiskEquipment
        {
            get => _dashboardRiskEquipment;
            set => this.RaiseAndSetIfChanged(ref _dashboardRiskEquipment, value);
        }

        public string DashboardRiskEquipmentValue
        {
            get => _dashboardRiskEquipmentValue;
            set => this.RaiseAndSetIfChanged(ref _dashboardRiskEquipmentValue, value);
        }

        public string DashboardCurrentPeriodMttr
        {
            get => _dashboardCurrentPeriodMttr;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodMttr, value);
        }

        public string DashboardCurrentPeriodMtbf
        {
            get => _dashboardCurrentPeriodMtbf;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodMtbf, value);
        }

        public double DashboardCurrentPeriodUnassignedSharePercent
        {
            get => _dashboardCurrentPeriodUnassignedSharePercent;
            set => this.RaiseAndSetIfChanged(ref _dashboardCurrentPeriodUnassignedSharePercent, value);
        }

        public string DashboardRecurringFailuresValue
        {
            get => _dashboardRecurringFailuresValue;
            set => this.RaiseAndSetIfChanged(ref _dashboardRecurringFailuresValue, value);
        }

        public ObservableCollection<SubdivisionRatingRow> DashboardSubdivisionRatings
        {
            get => _dashboardSubdivisionRatings;
            set
            {
                if (ReferenceEquals(_dashboardSubdivisionRatings, value))
                    return;
                var old = _dashboardSubdivisionRatings;
                _dashboardSubdivisionRatings = value;
                if (old != null)
                    old.CollectionChanged -= OnSubdivisionsChanged;
                if (value != null)
                    value.CollectionChanged += OnSubdivisionsChanged;
                this.RaisePropertyChanged();
            }
        }

        private void OnSubdivisionsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            this.RaisePropertyChanged(nameof(VisibleSubdivisionRatings));
            this.RaisePropertyChanged(nameof(SubdivisionRatingsCanCollapse));
            this.RaisePropertyChanged(nameof(SubdivisionRatingsShowAllVisible));
        }

        private bool _showAllSubdivisions;
        public bool ShowAllSubdivisions
        {
            get => _showAllSubdivisions;
            set
            {
                if (this.RaiseAndSetIfChanged(ref _showAllSubdivisions, value))
                {
                    this.RaisePropertyChanged(nameof(VisibleSubdivisionRatings));
                    this.RaisePropertyChanged(nameof(SubdivisionRatingsCollapsed));
                    this.RaisePropertyChanged(nameof(SubdivisionRatingsShowAllVisible));
                }
            }
        }

        /// <summary>True when the subdivision list is collapsed (showing top-5 only).</summary>
        public bool SubdivisionRatingsCollapsed => !_showAllSubdivisions;

        /// <summary>True when there are more than 5 subdivisions (i.e. collapse is meaningful).</summary>
        public bool SubdivisionRatingsCanCollapse => _dashboardSubdivisionRatings.Count > 5;

        /// <summary>True when the "Show all" button should be visible.</summary>
        public bool SubdivisionRatingsShowAllVisible => SubdivisionRatingsCollapsed && SubdivisionRatingsCanCollapse;

        /// <summary>Top-5 when collapsed, full list when expanded.</summary>
        public System.Collections.Generic.IEnumerable<SubdivisionRatingRow> VisibleSubdivisionRatings
        {
            get
            {
                if (_showAllSubdivisions || _dashboardSubdivisionRatings.Count <= 5)
                    return _dashboardSubdivisionRatings;
                var top = new System.Collections.Generic.List<SubdivisionRatingRow>(5);
                for (int i = 0; i < 5 && i < _dashboardSubdivisionRatings.Count; i++)
                    top.Add(_dashboardSubdivisionRatings[i]);
                return top;
            }
        }

        public string SelectedDashboardIssueTypeFilter
        {
            get => _selectedDashboardIssueTypeFilter;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? "Все типы" : value.Trim();
                if (string.Equals(_selectedDashboardIssueTypeFilter, normalized, StringComparison.CurrentCulture))
                    return;

                this.RaiseAndSetIfChanged(ref _selectedDashboardIssueTypeFilter, normalized);
                OnDashboardFiltersChanged();
            }
        }

        public string SelectedDashboardResponsibleFilter
        {
            get => _selectedDashboardResponsibleFilter;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? "Все ответственные" : value.Trim();
                if (string.Equals(_selectedDashboardResponsibleFilter, normalized, StringComparison.CurrentCulture))
                    return;

                this.RaiseAndSetIfChanged(ref _selectedDashboardResponsibleFilter, normalized);
                OnDashboardFiltersChanged();
            }
        }

        public string SelectedDashboardSubdivisionFilter
        {
            get => _selectedDashboardSubdivisionFilter;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? "Все группы" : value.Trim();
                if (string.Equals(_selectedDashboardSubdivisionFilter, normalized, StringComparison.CurrentCulture))
                    return;

                this.RaiseAndSetIfChanged(ref _selectedDashboardSubdivisionFilter, normalized);
                OnDashboardFiltersChanged();
            }
        }

        internal void RebuildResponsibleFilters()
        {
            var previous = SelectedDashboardResponsibleFilter;
            DashboardResponsibleFilters.Clear();
            DashboardResponsibleFilters.Add("Все ответственные");
            DashboardResponsibleFilters.Add("Без ответственного");

            foreach (var responsible in _shell.GetEquipmentForReports()
                .SelectMany(e => e.Issues)
                .Select(i => i.Responsible?.Trim())
                .Where(r => !string.IsNullOrWhiteSpace(r) && !string.Equals(r, "-", StringComparison.CurrentCultureIgnoreCase))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(r => r, StringComparer.CurrentCultureIgnoreCase))
            {
                DashboardResponsibleFilters.Add(responsible!);
            }

            _isRefreshingFilters = true;
            SelectedDashboardResponsibleFilter = DashboardResponsibleFilters.Contains(previous) ? previous : "Все ответственные";
            _isRefreshingFilters = false;
        }

        internal void RebuildSubdivisionFilters()
        {
            var previous = SelectedDashboardSubdivisionFilter;
            DashboardSubdivisionFilters.Clear();
            DashboardSubdivisionFilters.Add("Все группы");
            DashboardSubdivisionFilters.Add("Без группы");

            foreach (var subdivision in _shell.GetEquipmentForReports()
                .Select(e => e.Subdivision?.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase))
            {
                DashboardSubdivisionFilters.Add(subdivision!);
            }

            _isRefreshingFilters = true;
            SelectedDashboardSubdivisionFilter = DashboardSubdivisionFilters.Contains(previous) ? previous : "Все группы";
            _isRefreshingFilters = false;
        }

        private void ResetDashboardFilters()
        {
            _isRefreshingFilters = true;
            SelectedDashboardIssueTypeFilter = "Все типы";
            SelectedDashboardResponsibleFilter = "Все ответственные";
            SelectedDashboardSubdivisionFilter = "Все группы";
            _isRefreshingFilters = false;
            _shell.HandleDashboardFilterChanged();
            _shell.FiltersResetCounter += 1;
        }

        private void OnDashboardFiltersChanged()
        {
            if (_isRefreshingFilters)
                return;

            _shell.HandleDashboardFilterChanged();
        }
    }
}
