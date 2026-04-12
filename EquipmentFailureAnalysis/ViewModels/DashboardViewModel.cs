using EquipmentFailureAnalysis.Models;
using ReactiveUI;
using System.Collections.ObjectModel;

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
        private double _dashboardCurrentPeriodUnassignedSharePercent;
        private string _dashboardRecurringFailuresValue = "0 ед. / 0 событий";
        private ObservableCollection<SubdivisionRatingRow> _dashboardSubdivisionRatings = new();
        private ObservableCollection<DashboardTrendPoint> _dashboardMonthlyTrends = new();

        public DashboardViewModel(MainWindowViewModel shell)
        {
            _shell = shell;
        }

        public ObservableCollection<EquipmentInfo> EquipmentCollection => _shell.EquipmentCollection;

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
            set => this.RaiseAndSetIfChanged(ref _dashboardSubdivisionRatings, value);
        }
    }
}
