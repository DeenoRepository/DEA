using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using EquipmentFailureAnalysis.Services;
using EquipmentFailureAnalysis.Utility;
using System.Linq;
using System;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace EquipmentFailureAnalysis.Views
{
    public partial class MainWindow : Window
    {
        private readonly JiraSettingsStore _jiraSettingsStore = new JiraSettingsStore();
        private readonly HtmlReportService _htmlReportService = new HtmlReportService();
        private readonly JiraImportService _jiraImportService = new JiraImportService();
        private readonly XmlImportService _xmlImportService = new XmlImportService();
        private readonly DispatcherTimer _settingsSaveDebounceTimer;
        private bool _suppressSettingsSave;
        private INotifyPropertyChanged? _trackedSettingsVm;

        private DateTime _lastEquipmentMenuOpenUtc = DateTime.MinValue;
        private AppPage _currentPage = AppPage.Dashboard;

        private enum AppPage
        {
            Dashboard,
            FailureAnalysis,
            DowntimeAnalysis,
            Reports,
            Settings,
            EmployeeAnalysis
        }

        internal async void ImportFromJiraButton_Click(object? sender, RoutedEventArgs e)
        {
            var (url, username, token, jql, filterIds) = GetJiraApiSettingsFromUi();
            var result = await _jiraImportService.ImportAsync(new JiraImportRequest
            {
                Url = url,
                Username = username,
                Token = token,
                Jql = jql,
                FilterIds = filterIds
            });

            if (!result.Success)
            {
                var title = string.IsNullOrWhiteSpace(url) ? "Ошибка" : "Ошибка импорта Jira";
                await ShowMessageAsync(title, result.ErrorMessage);
                PublishStatus($"Ошибка импорта Jira: {result.ErrorMessage}");
                return;
            }

            ApplyParsedResults(result.Items);
            await ShowMessageAsync("Импорт из Jira", result.BuildSummaryMessage());
            PublishStatus($"Импорт Jira завершен: {result.Items.Count} ед. оборудования, {result.IssuesCount} событий.");
        }

        private void ApplyParsedResults(ObservableCollection<EquipmentFailureAnalysis.Models.EquipmentInfo> items)
        {
            if (this.DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            vm.ImportEquipment(items);
            _currentPage = AppPage.Dashboard;
            UpdatePageVisibility();
        }

        private (string Url, string Username, string Token, string Jql, IReadOnlyCollection<string> FilterIds) GetJiraApiSettingsFromUi()
        {
            var settings = (DataContext as EquipmentFailureAnalysis.ViewModels.MainWindowViewModel)?.Settings;
            var url = settings?.JiraResourceUrl?.Trim() ?? string.Empty;
            var username = settings?.JiraUsername?.Trim() ?? string.Empty;
            var token = settings?.JiraToken ?? string.Empty;
            var jql = settings?.JiraJql?.Trim() ?? string.Empty;
            return (url, username, token, jql, GetFilterIdsFromUi());
        }

        private IReadOnlyCollection<string> GetFilterIdsFromUi()
        {
            var list = ((DataContext as EquipmentFailureAnalysis.ViewModels.MainWindowViewModel)?.Settings?.JiraFilterIds ?? new ObservableCollection<string>())
                .Select(v => v?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .Where(v => v.All(char.IsDigit))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return list;
        }

        private async System.Threading.Tasks.Task ShowMessageAsync(string title, string message)
        {
            var wnd = new Window
            {
                Title = title,
                Width = 480,
                Height = 160,
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Thickness(14)
                }
            };

            await wnd.ShowDialog(this);
        }

        private void PublishStatus(string message)
        {
            if (DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                vm.AddStatusEvent(message);
        }

        private T? FindNestedControl<T>(string name) where T : Control
        {
            var direct = this.FindControl<T>(name);
            if (direct != null)
                return direct;

            var visual = this.GetVisualDescendants()
                .OfType<T>()
                .FirstOrDefault(control =>
                    control is StyledElement styled &&
                    string.Equals(styled.Name, name, StringComparison.Ordinal));
            if (visual != null)
                return visual;

            return this.GetLogicalDescendants()
                .OfType<T>()
                .FirstOrDefault(control =>
                    control is StyledElement styled &&
                    string.Equals(styled.Name, name, StringComparison.Ordinal));
        }

        public MainWindow()
        {
            _settingsSaveDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _settingsSaveDebounceTimer.Tick += (_, _) =>
            {
                _settingsSaveDebounceTimer.Stop();
                SaveJiraSettingsFromUi(immediate: true);
            };

            InitializeComponent();
            ApplyNavigationPanelState();
            InitializeReportTools();
            LoadJiraSettingsToUi();
            this.GetObservable<Rect>(BoundsProperty).Subscribe(_ => OnWindowResized());
            UpdatePageVisibility();
            PublishStatus("Приложение готово к работе.");
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveJiraSettingsFromUi(immediate: true);
            base.OnClosed(e);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            HookSettingsPersistence();
            LoadJiraSettingsToUi();
        }

        internal void JiraFilterIdAddButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            var settings = vm.Settings;
            var filterId = settings.JiraFilterIdInput?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(filterId) || !filterId.All(char.IsDigit))
                return;

            if (settings.JiraFilterIds.Any(v => string.Equals(v, filterId, StringComparison.Ordinal)))
                return;

            settings.JiraFilterIds.Add(filterId);
            settings.JiraFilterIdInput = string.Empty;
            SaveJiraSettingsFromUi();
        }

        internal void JiraFilterIdRemoveButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            var selected = vm.Settings.JiraSelectedFilterId;
            if (string.IsNullOrWhiteSpace(selected))
                return;

            vm.Settings.JiraFilterIds.Remove(selected);
            vm.Settings.JiraSelectedFilterId = null;
            SaveJiraSettingsFromUi();
        }

        internal void JiraSettingsField_TextChanged(object? sender, TextChangedEventArgs e)
        {
            SaveJiraSettingsFromUi();
        }

        internal void ReportSettingsChanged()
        {
            SaveJiraSettingsFromUi();
        }

        private sealed class JiraImportSettings
        {
            public string JiraResourceUrl { get; set; } = string.Empty;
            public string JiraUsername { get; set; } = string.Empty;
            public string JiraJql { get; set; } = string.Empty;
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
        }

        private static string NormalizeReportGroupByKey(string? value)
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

        private void SaveJiraSettingsFromUi(bool immediate = false)
        {
            if (_suppressSettingsSave)
                return;

            if (!immediate)
            {
                _settingsSaveDebounceTimer.Stop();
                _settingsSaveDebounceTimer.Start();
                return;
            }

            try
            {
                var existing = _jiraSettingsStore.TryLoad("jira_import_settings.json", out JiraImportSettings? loaded) && loaded != null
                    ? loaded
                    : new JiraImportSettings();

                var shellVm = DataContext as EquipmentFailureAnalysis.ViewModels.MainWindowViewModel;
                var settingsVm = shellVm?.Settings;
                var reportsVm = shellVm?.Reports;

                var settings = new JiraImportSettings
                {
                    JiraResourceUrl = settingsVm?.JiraResourceUrl?.Trim() ?? existing.JiraResourceUrl,
                    JiraUsername = settingsVm?.JiraUsername?.Trim() ?? existing.JiraUsername,
                    JiraJql = settingsVm?.JiraJql?.Trim() ?? existing.JiraJql,
                    JiraFilterIds = settingsVm != null ? GetFilterIdsFromUi().ToList() : (existing.JiraFilterIds ?? new List<string>()),
                    HeatmapSelectedSetting = shellVm?.SelectedHeatmapSetting ?? string.Empty,
                    FailureHeatmapMin = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.FailureHeatmapKey).Min,
                    FailureHeatmapMax = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.FailureHeatmapKey).Max,
                    DowntimeHeatmapMin = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.DowntimeHeatmapKey).Min,
                    DowntimeHeatmapMax = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.DowntimeHeatmapKey).Max,
                    ReportStartDate = reportsVm?.ReportStartDate?.Date ?? existing.ReportStartDate,
                    ReportEndDate = reportsVm?.ReportEndDate?.Date ?? existing.ReportEndDate,
                    ReportGroupBy = reportsVm != null ? NormalizeReportGroupByKey(reportsVm.ReportGroupByKey) : NormalizeReportGroupByKey(existing.ReportGroupBy),
                    ReportIncludeDashboard = reportsVm?.ReportIncludeDashboard ?? existing.ReportIncludeDashboard,
                    ReportIncludeDowntime = reportsVm?.ReportIncludeDowntime ?? existing.ReportIncludeDowntime,
                    ReportIncludeEmployee = reportsVm?.ReportIncludeEmployee ?? existing.ReportIncludeEmployee,
                    ReportOpenAfterGenerate = reportsVm?.ReportOpenAfterGenerate ?? existing.ReportOpenAfterGenerate,
                    ReportOnlyInProgress = reportsVm?.ReportOnlyInProgress ?? existing.ReportOnlyInProgress,
                    ReportFilterByDuration = reportsVm?.ReportFilterByDuration ?? existing.ReportFilterByDuration,
                    ReportMinDurationMinutes = reportsVm != null ? (int)Math.Round(reportsVm.ReportMinDurationMinutes) : existing.ReportMinDurationMinutes,
                    ReportFieldStart = reportsVm?.ReportFieldStart ?? existing.ReportFieldStart,
                    ReportFieldEnd = reportsVm?.ReportFieldEnd ?? existing.ReportFieldEnd,
                    ReportFieldEquipment = reportsVm?.ReportFieldEquipment ?? existing.ReportFieldEquipment,
                    ReportFieldSubdivision = reportsVm?.ReportFieldSubdivision ?? existing.ReportFieldSubdivision,
                    ReportFieldType = reportsVm?.ReportFieldType ?? existing.ReportFieldType,
                    ReportFieldResponsible = reportsVm?.ReportFieldResponsible ?? existing.ReportFieldResponsible,
                    ReportFieldDescription = reportsVm?.ReportFieldDescription ?? existing.ReportFieldDescription,
                    ReportLastFilePath = reportsVm?.ReportLastFilePath ?? existing.ReportLastFilePath
                };

                _jiraSettingsStore.Save("jira_import_settings.json", settings);
            }
            catch
            {
                // ignore persistence errors
            }
        }

        private void LoadJiraSettingsToUi()
        {
            try
            {
                _suppressSettingsSave = true;

                if (!_jiraSettingsStore.TryLoad("jira_import_settings.json", out JiraImportSettings? settings) || settings == null)
                    return;

                if (DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel shellSettingsVm)
                {
                    shellSettingsVm.Settings.JiraResourceUrl = settings.JiraResourceUrl ?? string.Empty;
                    shellSettingsVm.Settings.JiraUsername = settings.JiraUsername ?? string.Empty;
                    shellSettingsVm.Settings.JiraJql = settings.JiraJql ?? string.Empty;
                    shellSettingsVm.Settings.JiraFilterIds.Clear();
                    var loadedIds = settings.JiraFilterIds ?? new List<string>();
                    foreach (var filterId in loadedIds.Where(v => !string.IsNullOrWhiteSpace(v) && v.All(char.IsDigit)).Distinct(StringComparer.Ordinal))
                        shellSettingsVm.Settings.JiraFilterIds.Add(filterId.Trim());
                    shellSettingsVm.Settings.JiraSelectedFilterId = null;
                }

                if (DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                {
                    vm.SelectFailureHeatmapSettings();
                    vm.HeatmapColorMin = Math.Max(0, settings.FailureHeatmapMin);
                    vm.HeatmapColorMax = Math.Max(settings.FailureHeatmapMax, settings.FailureHeatmapMin + 1);

                    vm.SelectDowntimeHeatmapSettings();
                    vm.HeatmapColorMin = Math.Max(0, settings.DowntimeHeatmapMin);
                    vm.HeatmapColorMax = Math.Max(settings.DowntimeHeatmapMax, settings.DowntimeHeatmapMin + 1);

                    var selected = vm.HeatmapSettingOptions.FirstOrDefault(v => string.Equals(v, settings.HeatmapSelectedSetting, StringComparison.CurrentCultureIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(selected))
                        vm.SelectedHeatmapSetting = selected;
                }

                if (DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel reportOwnerVm)
                {
                    reportOwnerVm.Reports.ReportStartDate = settings.ReportStartDate ?? DateTime.Now.Date;
                    reportOwnerVm.Reports.ReportEndDate = settings.ReportEndDate ?? DateTime.Now.Date;
                    reportOwnerVm.Reports.ReportGroupByKey = NormalizeReportGroupByKey(settings.ReportGroupBy);
                    reportOwnerVm.Reports.ReportIncludeDashboard = settings.ReportIncludeDashboard;
                    reportOwnerVm.Reports.ReportIncludeDowntime = settings.ReportIncludeDowntime;
                    reportOwnerVm.Reports.ReportIncludeEmployee = settings.ReportIncludeEmployee;
                    reportOwnerVm.Reports.ReportOpenAfterGenerate = settings.ReportOpenAfterGenerate;
                    reportOwnerVm.Reports.ReportOnlyInProgress = settings.ReportOnlyInProgress;
                    reportOwnerVm.Reports.ReportFilterByDuration = settings.ReportFilterByDuration;
                    reportOwnerVm.Reports.ReportMinDurationMinutes = Math.Max(0, settings.ReportMinDurationMinutes);
                    reportOwnerVm.Reports.ReportFieldStart = settings.ReportFieldStart;
                    reportOwnerVm.Reports.ReportFieldEnd = settings.ReportFieldEnd;
                    reportOwnerVm.Reports.ReportFieldEquipment = settings.ReportFieldEquipment;
                    reportOwnerVm.Reports.ReportFieldSubdivision = settings.ReportFieldSubdivision;
                    reportOwnerVm.Reports.ReportFieldType = settings.ReportFieldType;
                    reportOwnerVm.Reports.ReportFieldResponsible = settings.ReportFieldResponsible;
                    reportOwnerVm.Reports.ReportFieldDescription = settings.ReportFieldDescription;
                    reportOwnerVm.Reports.ReportLastFilePath = settings.ReportLastFilePath ?? string.Empty;
                }
            }
            catch
            {
                // ignore invalid settings file
            }
            finally
            {
                _suppressSettingsSave = false;
            }
        }

    }
}
