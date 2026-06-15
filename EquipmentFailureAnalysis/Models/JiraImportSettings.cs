using System;
using System.Collections.Generic;

namespace EquipmentFailureAnalysis.Models
{
    public class JiraImportSettings
    {
        public string JiraResourceUrl { get; set; } = string.Empty;
        public string JiraUsername { get; set; } = string.Empty;
        public string JiraJql { get; set; } = string.Empty;
        public bool JiraAutoImportEnabled { get; set; }
        public int JiraAutoImportPeriodMinutes { get; set; } = 30;
        public DateTime? JiraLastSuccessfulImportUtc { get; set; }
        public List<string> JiraFilterIds { get; set; } = new List<string>();
        public string HeatmapSelectedSetting { get; set; } = string.Empty;
        public int FailureHeatmapMin { get; set; } = 0;
        public int FailureHeatmapMax { get; set; } = 10;
        public int DowntimeHeatmapMin { get; set; } = 0;
        public int DowntimeHeatmapMax { get; set; } = 10;
        public DateTime? ReportStartDate { get; set; }
        public DateTime? ReportEndDate { get; set; }
        public string ReportGroupBy { get; set; } = string.Empty;
        public bool ReportIncludeDashboard { get; set; } = true;
        public bool ReportIncludeDowntime { get; set; } = true;
        public bool ReportIncludeEmployee { get; set; } = true;
        public bool ReportOpenAfterGenerate { get; set; } = true;
        public bool ReportOnlyInProgress { get; set; }
        public bool ReportFilterByDuration { get; set; }
        public int ReportMinDurationMinutes { get; set; } = 60;
        public bool ReportFieldStart { get; set; } = true;
        public bool ReportFieldEnd { get; set; } = true;
        public bool ReportFieldEquipment { get; set; } = true;
        public bool ReportFieldSubdivision { get; set; } = true;
        public bool ReportFieldType { get; set; } = true;
        public bool ReportFieldResponsible { get; set; } = true;
        public bool ReportFieldDescription { get; set; } = true;
        public string ReportLastFilePath { get; set; } = string.Empty;
        public bool LdapAuthEnabled { get; set; }
        public string LdapServer { get; set; } = string.Empty;
        public int LdapPort { get; set; } = 389;
        public bool LdapUseSsl { get; set; }
        public string LdapDomain { get; set; } = string.Empty;
        public string LdapBaseDn { get; set; } = string.Empty;
        public string LdapLastUsername { get; set; } = string.Empty;

        // UI Persistence Settings
        public string LastActivePage { get; set; } = "Dashboard";
        public DateTime? AnalysisDate { get; set; }
        public DateTime? DowntimeAnalysisDate { get; set; }
        public DateTime? EmployeeTimelineDate { get; set; }
        public int? SelectedEquipmentUid { get; set; }
        public string SelectedIssueTypeFilter { get; set; } = "Все позиции";
        public string SelectedDowntimeIssueTypeFilter { get; set; } = "Все типы";
        public string SelectedDowntimeStatusFilter { get; set; } = "Все статусы";
        public string SelectedDowntimeResponsibleFilter { get; set; } = "Все ответственные";
        public string SelectedDowntimeSubdivisionFilter { get; set; } = "Все группы";
        public string DowntimeEquipmentSearchQuery { get; set; } = string.Empty;
        public string SelectedDashboardIssueTypeFilter { get; set; } = "Все типы";
        public string SelectedDashboardResponsibleFilter { get; set; } = "Все ответственные";
        public string SelectedDashboardSubdivisionFilter { get; set; } = "Все группы";
        public string SelectedEmployeeTimelineEmployee { get; set; } = "Все сотрудники";
    }
}
