using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
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
        private readonly ObservableCollection<string> _jiraFilterIds = new ObservableCollection<string>();
        private readonly JiraSettingsStore _jiraSettingsStore = new JiraSettingsStore();
        private readonly HtmlReportService _htmlReportService = new HtmlReportService();
        private readonly JiraImportService _jiraImportService = new JiraImportService();
        private readonly XmlImportService _xmlImportService = new XmlImportService();
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
            var url = FindNestedControl<TextBox>("JiraResourceUrlBox")?.Text?.Trim() ?? string.Empty;
            var username = FindNestedControl<TextBox>("JiraUsernameBox")?.Text?.Trim() ?? string.Empty;
            var token = FindNestedControl<TextBox>("JiraTokenBox")?.Text ?? string.Empty;
            var jql = FindNestedControl<TextBox>("JiraJqlBox")?.Text?.Trim() ?? string.Empty;
            return (url, username, token, jql, GetFilterIdsFromUi());
        }

        private IReadOnlyCollection<string> GetFilterIdsFromUi()
        {
            var list = _jiraFilterIds
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
            InitializeComponent();
            ApplyNavigationPanelState();
            InitializeReportTools();
            var list = FindNestedControl<ListBox>("JiraFilterIdsList");
            if (list != null)
                list.ItemsSource = _jiraFilterIds;
            LoadJiraSettingsToUi();
            this.GetObservable<Rect>(BoundsProperty).Subscribe(_ => OnWindowResized());
            UpdatePageVisibility();
            PublishStatus("Приложение готово к работе.");
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            HookSettingsPersistence();
            LoadJiraSettingsToUi();
        }

        internal void JiraFilterIdAddButton_Click(object? sender, RoutedEventArgs e)
        {
            var box = FindNestedControl<TextBox>("JiraFilterIdBox");
            var filterId = box?.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(filterId) || !filterId.All(char.IsDigit))
                return;

            if (_jiraFilterIds.Any(v => string.Equals(v, filterId, StringComparison.Ordinal)))
                return;

            _jiraFilterIds.Add(filterId);
            if (box != null)
                box.Text = string.Empty;
            SaveJiraSettingsFromUi();
        }

        internal void JiraFilterIdRemoveButton_Click(object? sender, RoutedEventArgs e)
        {
            var list = FindNestedControl<ListBox>("JiraFilterIdsList");
            if (list?.SelectedItem is not string selected || string.IsNullOrWhiteSpace(selected))
                return;

            _jiraFilterIds.Remove(selected);
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

        private string GetReportGroupByKeyFromUi()
        {
            var combo = FindNestedControl<ComboBox>("ReportGroupByCombo");
            if (combo?.SelectedItem is ComboBoxItem selected)
            {
                var tagKey = selected.Tag?.ToString();
                if (!string.IsNullOrWhiteSpace(tagKey))
                    return NormalizeReportGroupByKey(tagKey);

                return NormalizeReportGroupByKey(selected.Content?.ToString());
            }

            return "day";
        }

        private void SaveJiraSettingsFromUi()
        {
            if (_suppressSettingsSave)
                return;

            try
            {
                var existing = _jiraSettingsStore.TryLoad("jira_import_settings.json", out JiraImportSettings? loaded) && loaded != null
                    ? loaded
                    : new JiraImportSettings();

                var jiraUrlBox = FindNestedControl<TextBox>("JiraResourceUrlBox");
                var jiraUsernameBox = FindNestedControl<TextBox>("JiraUsernameBox");
                var jiraJqlBox = FindNestedControl<TextBox>("JiraJqlBox");
                var jiraFilterIdsList = FindNestedControl<ListBox>("JiraFilterIdsList");

                var reportStartPicker = FindNestedControl<CalendarDatePicker>("ReportStartDatePicker");
                var reportEndPicker = FindNestedControl<CalendarDatePicker>("ReportEndDatePicker");
                var reportGroupByCombo = FindNestedControl<ComboBox>("ReportGroupByCombo");
                var reportIncludeDashboard = FindNestedControl<CheckBox>("ReportIncludeDashboardCheckBox");
                var reportIncludeDowntime = FindNestedControl<CheckBox>("ReportIncludeDowntimeCheckBox");
                var reportIncludeEmployee = FindNestedControl<CheckBox>("ReportIncludeEmployeeCheckBox");
                var reportOpenAfterGenerate = FindNestedControl<CheckBox>("ReportOpenAfterGenerateCheckBox");
                var reportOnlyInProgress = FindNestedControl<CheckBox>("ReportOnlyInProgressCheckBox");
                var reportFilterByDuration = FindNestedControl<CheckBox>("ReportFilterByDurationCheckBox");
                var reportMinDuration = FindNestedControl<NumericUpDown>("ReportMinDurationMinutesUpDown");
                var reportFieldStart = FindNestedControl<CheckBox>("ReportFieldStartCheckBox");
                var reportFieldEnd = FindNestedControl<CheckBox>("ReportFieldEndCheckBox");
                var reportFieldEquipment = FindNestedControl<CheckBox>("ReportFieldEquipmentCheckBox");
                var reportFieldSubdivision = FindNestedControl<CheckBox>("ReportFieldSubdivisionCheckBox");
                var reportFieldType = FindNestedControl<CheckBox>("ReportFieldTypeCheckBox");
                var reportFieldResponsible = FindNestedControl<CheckBox>("ReportFieldResponsibleCheckBox");
                var reportFieldDescription = FindNestedControl<CheckBox>("ReportFieldDescriptionCheckBox");
                var reportLastPathBox = FindNestedControl<TextBox>("ReportLastFilePathBox");

                var settings = new JiraImportSettings
                {
                    JiraResourceUrl = jiraUrlBox?.Text?.Trim() ?? existing.JiraResourceUrl,
                    JiraUsername = jiraUsernameBox?.Text?.Trim() ?? existing.JiraUsername,
                    JiraJql = jiraJqlBox?.Text?.Trim() ?? existing.JiraJql,
                    JiraFilterIds = jiraFilterIdsList != null ? GetFilterIdsFromUi().ToList() : (existing.JiraFilterIds ?? new List<string>()),
                    HeatmapSelectedSetting = (DataContext as EquipmentFailureAnalysis.ViewModels.MainWindowViewModel)?.SelectedHeatmapSetting ?? string.Empty,
                    FailureHeatmapMin = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.FailureHeatmapKey).Min,
                    FailureHeatmapMax = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.FailureHeatmapKey).Max,
                    DowntimeHeatmapMin = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.DowntimeHeatmapKey).Min,
                    DowntimeHeatmapMax = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.DowntimeHeatmapKey).Max,
                    ReportStartDate = reportStartPicker?.SelectedDate?.Date ?? existing.ReportStartDate,
                    ReportEndDate = reportEndPicker?.SelectedDate?.Date ?? existing.ReportEndDate,
                    ReportGroupBy = reportGroupByCombo != null ? GetReportGroupByKeyFromUi() : NormalizeReportGroupByKey(existing.ReportGroupBy),
                    ReportIncludeDashboard = reportIncludeDashboard != null ? reportIncludeDashboard.IsChecked == true : existing.ReportIncludeDashboard,
                    ReportIncludeDowntime = reportIncludeDowntime != null ? reportIncludeDowntime.IsChecked == true : existing.ReportIncludeDowntime,
                    ReportIncludeEmployee = reportIncludeEmployee != null ? reportIncludeEmployee.IsChecked == true : existing.ReportIncludeEmployee,
                    ReportOpenAfterGenerate = reportOpenAfterGenerate != null ? reportOpenAfterGenerate.IsChecked == true : existing.ReportOpenAfterGenerate,
                    ReportOnlyInProgress = reportOnlyInProgress != null ? reportOnlyInProgress.IsChecked == true : existing.ReportOnlyInProgress,
                    ReportFilterByDuration = reportFilterByDuration != null ? reportFilterByDuration.IsChecked == true : existing.ReportFilterByDuration,
                    ReportMinDurationMinutes = reportMinDuration != null ? (int)(reportMinDuration.Value ?? 60m) : existing.ReportMinDurationMinutes,
                    ReportFieldStart = reportFieldStart != null ? reportFieldStart.IsChecked != false : existing.ReportFieldStart,
                    ReportFieldEnd = reportFieldEnd != null ? reportFieldEnd.IsChecked != false : existing.ReportFieldEnd,
                    ReportFieldEquipment = reportFieldEquipment != null ? reportFieldEquipment.IsChecked != false : existing.ReportFieldEquipment,
                    ReportFieldSubdivision = reportFieldSubdivision != null ? reportFieldSubdivision.IsChecked != false : existing.ReportFieldSubdivision,
                    ReportFieldType = reportFieldType != null ? reportFieldType.IsChecked != false : existing.ReportFieldType,
                    ReportFieldResponsible = reportFieldResponsible != null ? reportFieldResponsible.IsChecked != false : existing.ReportFieldResponsible,
                    ReportFieldDescription = reportFieldDescription != null ? reportFieldDescription.IsChecked != false : existing.ReportFieldDescription,
                    ReportLastFilePath = reportLastPathBox?.Text?.Trim() ?? existing.ReportLastFilePath
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

                var urlBox = FindNestedControl<TextBox>("JiraResourceUrlBox");
                if (urlBox != null)
                    urlBox.Text = settings.JiraResourceUrl ?? string.Empty;

                var usernameBox = FindNestedControl<TextBox>("JiraUsernameBox");
                if (usernameBox != null)
                    usernameBox.Text = settings.JiraUsername ?? string.Empty;

                var jqlBox = FindNestedControl<TextBox>("JiraJqlBox");
                if (jqlBox != null)
                    jqlBox.Text = settings.JiraJql ?? string.Empty;

                _jiraFilterIds.Clear();
                var loadedIds = settings.JiraFilterIds ?? new List<string>();
                foreach (var filterId in loadedIds.Where(v => !string.IsNullOrWhiteSpace(v) && v.All(char.IsDigit)).Distinct(StringComparer.Ordinal))
                    _jiraFilterIds.Add(filterId.Trim());

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

                var reportStartPicker = FindNestedControl<CalendarDatePicker>("ReportStartDatePicker");
                if (reportStartPicker != null && settings.ReportStartDate.HasValue)
                    reportStartPicker.SelectedDate = settings.ReportStartDate.Value.Date;

                var reportEndPicker = FindNestedControl<CalendarDatePicker>("ReportEndDatePicker");
                if (reportEndPicker != null && settings.ReportEndDate.HasValue)
                    reportEndPicker.SelectedDate = settings.ReportEndDate.Value.Date;

                var reportGroupByCombo = FindNestedControl<ComboBox>("ReportGroupByCombo");
                if (reportGroupByCombo != null && !string.IsNullOrWhiteSpace(settings.ReportGroupBy))
                {
                    var desiredKey = NormalizeReportGroupByKey(settings.ReportGroupBy);
                    var item = reportGroupByCombo.Items
                        .OfType<ComboBoxItem>()
                        .FirstOrDefault(x => string.Equals(NormalizeReportGroupByKey(x.Tag?.ToString()), desiredKey, StringComparison.OrdinalIgnoreCase))
                        ?? reportGroupByCombo.Items
                            .OfType<ComboBoxItem>()
                            .FirstOrDefault(x => string.Equals(NormalizeReportGroupByKey(x.Content?.ToString()), desiredKey, StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                        reportGroupByCombo.SelectedItem = item;
                }

                var includeDashboard = FindNestedControl<CheckBox>("ReportIncludeDashboardCheckBox");
                if (includeDashboard != null)
                    includeDashboard.IsChecked = settings.ReportIncludeDashboard;

                var includeDowntime = FindNestedControl<CheckBox>("ReportIncludeDowntimeCheckBox");
                if (includeDowntime != null)
                    includeDowntime.IsChecked = settings.ReportIncludeDowntime;

                var includeEmployee = FindNestedControl<CheckBox>("ReportIncludeEmployeeCheckBox");
                if (includeEmployee != null)
                    includeEmployee.IsChecked = settings.ReportIncludeEmployee;

                var openAfterGenerate = FindNestedControl<CheckBox>("ReportOpenAfterGenerateCheckBox");
                if (openAfterGenerate != null)
                    openAfterGenerate.IsChecked = settings.ReportOpenAfterGenerate;

                var onlyInProgress = FindNestedControl<CheckBox>("ReportOnlyInProgressCheckBox");
                if (onlyInProgress != null)
                    onlyInProgress.IsChecked = settings.ReportOnlyInProgress;

                var filterByDuration = FindNestedControl<CheckBox>("ReportFilterByDurationCheckBox");
                if (filterByDuration != null)
                    filterByDuration.IsChecked = settings.ReportFilterByDuration;

                var minDurationControl = FindNestedControl<NumericUpDown>("ReportMinDurationMinutesUpDown");
                if (minDurationControl != null)
                    minDurationControl.Value = Math.Max(0, settings.ReportMinDurationMinutes);

                var fieldStart = FindNestedControl<CheckBox>("ReportFieldStartCheckBox");
                if (fieldStart != null)
                    fieldStart.IsChecked = settings.ReportFieldStart;

                var fieldEnd = FindNestedControl<CheckBox>("ReportFieldEndCheckBox");
                if (fieldEnd != null)
                    fieldEnd.IsChecked = settings.ReportFieldEnd;

                var fieldEquipment = FindNestedControl<CheckBox>("ReportFieldEquipmentCheckBox");
                if (fieldEquipment != null)
                    fieldEquipment.IsChecked = settings.ReportFieldEquipment;

                var fieldSubdivision = FindNestedControl<CheckBox>("ReportFieldSubdivisionCheckBox");
                if (fieldSubdivision != null)
                    fieldSubdivision.IsChecked = settings.ReportFieldSubdivision;

                var fieldType = FindNestedControl<CheckBox>("ReportFieldTypeCheckBox");
                if (fieldType != null)
                    fieldType.IsChecked = settings.ReportFieldType;

                var fieldResponsible = FindNestedControl<CheckBox>("ReportFieldResponsibleCheckBox");
                if (fieldResponsible != null)
                    fieldResponsible.IsChecked = settings.ReportFieldResponsible;

                var fieldDescription = FindNestedControl<CheckBox>("ReportFieldDescriptionCheckBox");
                if (fieldDescription != null)
                    fieldDescription.IsChecked = settings.ReportFieldDescription;

                var reportLastPathBox = FindNestedControl<TextBox>("ReportLastFilePathBox");
                if (reportLastPathBox != null)
                    reportLastPathBox.Text = settings.ReportLastFilePath ?? string.Empty;
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
