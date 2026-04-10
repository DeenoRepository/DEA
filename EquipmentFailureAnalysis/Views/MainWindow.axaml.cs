using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.VisualTree;
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

        private sealed class ReportIssueRow
        {
            public DateTime Start { get; init; }
            public DateTime End { get; init; }
            public string EquipmentTitle { get; init; } = string.Empty;
            public string InventoryNumber { get; init; } = string.Empty;
            public string Subdivision { get; init; } = string.Empty;
            public string Responsible { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public EquipmentFailureAnalysis.Models.IssueType Type { get; init; }
            public bool IsInProgress { get; init; }
            public double DurationMinutes => Math.Max(0, (End - Start).TotalMinutes);
        }

        private async void ImportFromJiraButton_Click(object? sender, RoutedEventArgs e)
        {
            var (url, username, token, jql, filterIds) = GetJiraApiSettingsFromUi();

            if (string.IsNullOrWhiteSpace(url))
            {
                await ShowMessageAsync("Ошибка", "Укажите URL Jira API.");
                return;
            }

            try
            {
                var decoder = new JiraApiDataDecoder();
                var effectiveJql = BuildEffectiveJql(jql, filterIds);
                var items = await decoder.DecodeEquipmentAsync(url, username, token, effectiveJql);
                ApplyParsedResults(items);

                var issuesCount = items.Sum(e => e.Issues?.Count ?? 0);
                var sample = items
                    .OrderByDescending(e => e.Issues?.Count ?? 0)
                    .Take(5)
                    .Select(e => $"• {e} — {e.Issues.Count} событий")
                    .ToList();

                var sampleText = sample.Count == 0
                    ? "Нет данных."
                    : string.Join(Environment.NewLine, sample);

                await ShowMessageAsync(
                    "Импорт из Jira",
                    $"Импорт завершен. Всего позиций: {decoder.LastTotalPositionsFromApi}, загружено: {decoder.LastLoadedPositionsFromApi}. Найдено оборудования: {items.Count}, событий: {issuesCount}.{Environment.NewLine}{Environment.NewLine}{sampleText}");
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Ошибка импорта Jira", ex.Message);
            }
        }

        private void ApplyParsedResults(ObservableCollection<EquipmentFailureAnalysis.Models.EquipmentInfo> items)
        {
            if (this.DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            vm.ImportEquipment(items);
            try
            {
                PersistedDataStore.SaveJiraImportedEquipment(items);
            }
            catch
            {
                // ignore persistence errors
            }
            _currentPage = AppPage.Dashboard;
            UpdatePageVisibility();
        }

        private (string Url, string Username, string Token, string Jql, IReadOnlyCollection<string> FilterIds) GetJiraApiSettingsFromUi()
        {
            var url = this.FindControl<TextBox>("JiraResourceUrlBox")?.Text?.Trim() ?? string.Empty;
            var username = this.FindControl<TextBox>("JiraUsernameBox")?.Text?.Trim() ?? string.Empty;
            var token = this.FindControl<TextBox>("JiraTokenBox")?.Text ?? string.Empty;
            var jql = this.FindControl<TextBox>("JiraJqlBox")?.Text?.Trim() ?? string.Empty;
            return (url, username, token, jql, GetFilterIdsFromUi());
        }

        private static string BuildEffectiveJql(string jql, IReadOnlyCollection<string> filterIds)
        {
            if (filterIds == null || filterIds.Count == 0)
                return jql;

            var filterClause = string.Join(" OR ", filterIds.Select(id => $"filter = {id}"));
            var fromFilters = $"({filterClause}) AND status in ('Решен', 'В процессе', 'Resolved', 'In Progress')";

            if (string.IsNullOrWhiteSpace(jql))
                return fromFilters;

            return $"({fromFilters}) AND ({jql.Trim()})";
        }

        private IReadOnlyCollection<string> GetFilterIdsFromUi()
        {
            var list = _jiraFilterIds
                .Select(v => v?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
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

        public MainWindow()
        {
            InitializeComponent();
            InitializeReportTools();
            var list = this.FindControl<ListBox>("JiraFilterIdsList");
            if (list != null)
                list.ItemsSource = _jiraFilterIds;
            LoadJiraSettingsToUi();
            this.GetObservable<Rect>(BoundsProperty).Subscribe(_ => OnWindowResized());
            UpdatePageVisibility();
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            HookSettingsPersistence();
            LoadJiraSettingsToUi();
        }

        private void JiraFilterIdAddButton_Click(object? sender, RoutedEventArgs e)
        {
            var box = this.FindControl<TextBox>("JiraFilterIdBox");
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

        private void JiraFilterIdRemoveButton_Click(object? sender, RoutedEventArgs e)
        {
            var list = this.FindControl<ListBox>("JiraFilterIdsList");
            if (list?.SelectedItem is not string selected || string.IsNullOrWhiteSpace(selected))
                return;

            _jiraFilterIds.Remove(selected);
            SaveJiraSettingsFromUi();
        }

        private void JiraSettingsField_TextChanged(object? sender, TextChangedEventArgs e)
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
        }

        private string GetJiraSettingsFile()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EquipmentFailureAnalysis");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return Path.Combine(dir, "jira_import_settings.json");
        }

        private void SaveJiraSettingsFromUi()
        {
            try
            {
                var settings = new JiraImportSettings
                {
                    JiraResourceUrl = this.FindControl<TextBox>("JiraResourceUrlBox")?.Text?.Trim() ?? string.Empty,
                    JiraUsername = this.FindControl<TextBox>("JiraUsernameBox")?.Text?.Trim() ?? string.Empty,
                    JiraJql = this.FindControl<TextBox>("JiraJqlBox")?.Text?.Trim() ?? string.Empty,
                    JiraFilterIds = GetFilterIdsFromUi().ToList(),
                    HeatmapSelectedSetting = (DataContext as EquipmentFailureAnalysis.ViewModels.MainWindowViewModel)?.SelectedHeatmapSetting ?? string.Empty,
                    FailureHeatmapMin = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.FailureHeatmapKey).Min,
                    FailureHeatmapMax = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.FailureHeatmapKey).Max,
                    DowntimeHeatmapMin = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.DowntimeHeatmapKey).Min,
                    DowntimeHeatmapMax = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.DowntimeHeatmapKey).Max
                };

                var json = JsonSerializer.Serialize(settings);
                File.WriteAllText(GetJiraSettingsFile(), json);
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

                var file = GetJiraSettingsFile();
                if (!File.Exists(file))
                    return;

                var json = File.ReadAllText(file);
                var settings = JsonSerializer.Deserialize<JiraImportSettings>(json);
                if (settings == null)
                    return;

                var urlBox = this.FindControl<TextBox>("JiraResourceUrlBox");
                if (urlBox != null)
                    urlBox.Text = settings.JiraResourceUrl ?? string.Empty;

                var usernameBox = this.FindControl<TextBox>("JiraUsernameBox");
                if (usernameBox != null)
                    usernameBox.Text = settings.JiraUsername ?? string.Empty;

                var jqlBox = this.FindControl<TextBox>("JiraJqlBox");
                if (jqlBox != null)
                    jqlBox.Text = settings.JiraJql ?? string.Empty;

                _jiraFilterIds.Clear();
                var loadedIds = settings.JiraFilterIds ?? new List<string>();
                foreach (var filterId in loadedIds.Where(v => !string.IsNullOrWhiteSpace(v) && v.All(char.IsDigit)).Distinct(StringComparer.Ordinal))
                    _jiraFilterIds.Add(filterId.Trim());

                if (DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                {
                    var failureOption = vm.HeatmapSettingOptions.FirstOrDefault(v => v.Contains("неисправ", StringComparison.CurrentCultureIgnoreCase));
                    var downtimeOption = vm.HeatmapSettingOptions.FirstOrDefault(v => v.Contains("просто", StringComparison.CurrentCultureIgnoreCase));

                    if (!string.IsNullOrWhiteSpace(failureOption))
                    {
                        vm.SelectedHeatmapSetting = failureOption;
                        vm.HeatmapColorMin = Math.Max(0, settings.FailureHeatmapMin);
                        vm.HeatmapColorMax = Math.Max(settings.FailureHeatmapMax, settings.FailureHeatmapMin + 1);
                    }

                    if (!string.IsNullOrWhiteSpace(downtimeOption))
                    {
                        vm.SelectedHeatmapSetting = downtimeOption;
                        vm.HeatmapColorMin = Math.Max(0, settings.DowntimeHeatmapMin);
                        vm.HeatmapColorMax = Math.Max(settings.DowntimeHeatmapMax, settings.DowntimeHeatmapMin + 1);
                    }

                    var selected = vm.HeatmapSettingOptions.FirstOrDefault(v => string.Equals(v, settings.HeatmapSelectedSetting, StringComparison.CurrentCultureIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(selected))
                        vm.SelectedHeatmapSetting = selected;
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

        private void HookSettingsPersistence()
        {
            if (_trackedSettingsVm != null)
                _trackedSettingsVm.PropertyChanged -= OnViewModelPropertyChanged;

            _trackedSettingsVm = DataContext as INotifyPropertyChanged;
            if (_trackedSettingsVm != null)
                _trackedSettingsVm.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressSettingsSave)
                return;

            if (e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.HeatmapColorMin)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.HeatmapColorMax)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.SelectedHeatmapSetting))
            {
                SaveJiraSettingsFromUi();
            }
        }

        private void FailureAnalysisButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentPage = AppPage.FailureAnalysis;
            UpdatePageVisibility();
        }

        private void DashboardButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentPage = AppPage.Dashboard;
            UpdatePageVisibility();
        }

        private void DowntimeAnalysisButton_Click(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
            {
                vm.ShowDowntimeDayCommand.Execute(vm.DowntimeAnalysisDate).Subscribe();
            }

            _currentPage = AppPage.DowntimeAnalysis;
            UpdatePageVisibility();
        }

        private void SettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentPage = AppPage.Settings;
            UpdatePageVisibility();
        }

        private void ReportsButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentPage = AppPage.Reports;
            UpdatePageVisibility();
        }

        private void InitializeReportTools()
        {
            var startPicker = this.FindControl<CalendarDatePicker>("ReportStartDatePicker");
            var endPicker = this.FindControl<CalendarDatePicker>("ReportEndDatePicker");
            if (startPicker != null && startPicker.SelectedDate == null)
                startPicker.SelectedDate = DateTime.Now.Date.AddDays(-30);
            if (endPicker != null && endPicker.SelectedDate == null)
                endPicker.SelectedDate = DateTime.Now.Date;
        }

        private async void GenerateHtmlReportButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            var startPicker = this.FindControl<CalendarDatePicker>("ReportStartDatePicker");
            var endPicker = this.FindControl<CalendarDatePicker>("ReportEndDatePicker");
            var groupByCombo = this.FindControl<ComboBox>("ReportGroupByCombo");
            var includeDashboard = this.FindControl<CheckBox>("ReportIncludeDashboardCheckBox")?.IsChecked == true;
            var includeDowntime = this.FindControl<CheckBox>("ReportIncludeDowntimeCheckBox")?.IsChecked == true;
            var includeEmployee = this.FindControl<CheckBox>("ReportIncludeEmployeeCheckBox")?.IsChecked == true;
            var openAfterGenerate = this.FindControl<CheckBox>("ReportOpenAfterGenerateCheckBox")?.IsChecked == true;
            var onlyInProgress = this.FindControl<CheckBox>("ReportOnlyInProgressCheckBox")?.IsChecked == true;
            var outputPathBox = this.FindControl<TextBox>("ReportLastFilePathBox");

            var startDate = startPicker?.SelectedDate?.Date ?? DateTime.Now.Date.AddDays(-30);
            var endDate = endPicker?.SelectedDate?.Date ?? DateTime.Now.Date;
            if (endDate < startDate)
            {
                await ShowMessageAsync("Отчеты", "Дата окончания периода не может быть меньше даты начала.");
                return;
            }

            var periodEndExclusive = endDate.AddDays(1);
            var reportRows = vm.GetEquipmentForReports()
                .SelectMany(eq => (eq.Issues ?? new ObservableCollection<EquipmentFailureAnalysis.Models.Issue>())
                    .Where(issue => issue.End > startDate && issue.Start < periodEndExclusive)
                    .Select(issue => new ReportIssueRow
                    {
                        Start = issue.Start,
                        End = issue.End,
                        EquipmentTitle = eq.Title ?? string.Empty,
                        InventoryNumber = eq.InventoryNumber ?? string.Empty,
                        Subdivision = eq.Subdivision ?? string.Empty,
                        Responsible = string.IsNullOrWhiteSpace(issue.Responsible) ? "Без ответственного" : issue.Responsible!.Trim(),
                        Description = issue.Description ?? string.Empty,
                        Type = issue.Type,
                        IsInProgress = issue.IsInProgress
                    }))
                .ToList();

            if (onlyInProgress)
            {
                reportRows = reportRows
                    .Where(r => r.IsInProgress)
                    .ToList();
            }

            string groupBy = "По дням";
            if (groupByCombo?.SelectedItem is ComboBoxItem selectedGroup)
                groupBy = selectedGroup.Content?.ToString() ?? groupBy;

            var ru = new CultureInfo("ru-RU");
            var grouped = reportRows
                .GroupBy(row => groupBy switch
                {
                    "По месяцам" => new DateTime(row.Start.Year, row.Start.Month, 1).ToString("MMMM yyyy", ru),
                    "По сотрудникам" => row.Responsible,
                    "По оборудованию" => string.IsNullOrWhiteSpace(row.InventoryNumber) ? row.EquipmentTitle : $"{row.EquipmentTitle} ({row.InventoryNumber})",
                    _ => row.Start.Date.ToString("dd.MM.yyyy")
                })
                .Select(g => new
                {
                    GroupName = g.Key,
                    Total = g.Count(),
                    Repairs = g.Count(x => x.Type == EquipmentFailureAnalysis.Models.IssueType.Ремонт),
                    Setups = g.Count(x => x.Type == EquipmentFailureAnalysis.Models.IssueType.Настройка),
                    AvgMinutes = g.Any() ? g.Average(x => x.DurationMinutes) : 0.0,
                    TotalMinutes = g.Sum(x => x.DurationMinutes)
                })
                .OrderByDescending(x => x.Total)
                .ThenBy(x => x.GroupName)
                .ToList();

            string H(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
            var html = new StringBuilder();
            html.AppendLine("<!doctype html>");
            html.AppendLine("<html lang=\"ru\"><head><meta charset=\"utf-8\" />");
            html.AppendLine("<title>Отчет DEA</title>");
            html.AppendLine("<style>body{font-family:'Segoe UI',Arial,sans-serif;background:#f4f7fb;color:#1f2937;margin:24px}h1,h2{margin:0 0 10px}section{background:#fff;border:1px solid #e5eaf0;padding:14px;margin:0 0 12px}table{width:100%;border-collapse:collapse}th,td{border-bottom:1px solid #edf1f5;padding:6px 8px;text-align:left}th{background:#f8fafc} .muted{color:#6b7280;font-size:12px}</style>");
            html.AppendLine("</head><body>");
            html.AppendLine($"<h1>Отчет по метрикам оборудования</h1><div class=\"muted\">Период: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}. Сформирован: {DateTime.Now:dd.MM.yyyy HH:mm}</div>");
            if (onlyInProgress)
                html.AppendLine("<div class=\"muted\">Режим отчета: только задачи в процессе на момент формирования.</div>");

            if (includeDashboard)
            {
                html.AppendLine("<section><h2>Панель управления</h2><table><tbody>");
                html.AppendLine($"<tr><th>События (30 дней)</th><td>{vm.DashboardCurrentPeriodIssues}</td></tr>");
                html.AppendLine($"<tr><th>SLA</th><td>{vm.DashboardCurrentPeriodSlaCompliancePercent:0.#}%</td></tr>");
                html.AppendLine($"<tr><th>Средняя длительность</th><td>{H(vm.DashboardCurrentPeriodAvgDuration)}</td></tr>");
                html.AppendLine($"<tr><th>Зона внимания</th><td>{H(vm.DashboardRiskEquipment)} ({H(vm.DashboardRiskEquipmentValue)})</td></tr>");
                html.AppendLine("</tbody></table></section>");
            }

            if (includeDowntime)
            {
                html.AppendLine("<section><h2>Анализ простоев</h2><table><tbody>");
                html.AppendLine($"<tr><th>События</th><td>{vm.DowntimeTotalIssues}</td></tr>");
                html.AppendLine($"<tr><th>Ремонты</th><td>{vm.DowntimeRepairsCount}</td></tr>");
                html.AppendLine($"<tr><th>Настройки</th><td>{vm.DowntimeSetupsCount}</td></tr>");
                html.AppendLine($"<tr><th>Суммарный простой</th><td>{H(vm.DowntimeTotalDuration)}</td></tr>");
                html.AppendLine("</tbody></table></section>");
            }

            if (includeEmployee)
            {
                html.AppendLine("<section><h2>Анализ сотрудников</h2><table><tbody>");
                html.AppendLine($"<tr><th>Сотрудники</th><td>{vm.EmployeeTotalCount}</td></tr>");
                html.AppendLine($"<tr><th>События</th><td>{vm.EmployeeTotalIssues}</td></tr>");
                html.AppendLine($"<tr><th>SLA</th><td>{vm.EmployeeSlaCompliancePercent:0.#}%</td></tr>");
                html.AppendLine($"<tr><th>Лидер по событиям</th><td>{H(vm.EmployeeTopByIssues)} ({H(vm.EmployeeTopByIssuesValue)})</td></tr>");
                html.AppendLine("</tbody></table></section>");
            }

            html.AppendLine($"<section><h2>Группировка: {H(groupBy)}</h2><table><thead><tr><th>Группа</th><th>События</th><th>Рем.</th><th>Наст.</th><th>Ср. длительность</th><th>Суммарно</th></tr></thead><tbody>");
            foreach (var g in grouped)
                html.AppendLine($"<tr><td>{H(g.GroupName)}</td><td>{g.Total}</td><td>{g.Repairs}</td><td>{g.Setups}</td><td>{TimeSpan.FromMinutes(g.AvgMinutes):hh\\:mm}</td><td>{TimeSpan.FromMinutes(g.TotalMinutes):hh\\:mm}</td></tr>");
            if (grouped.Count == 0)
                html.AppendLine("<tr><td colspan=\"6\">Нет данных за выбранный период.</td></tr>");
            html.AppendLine("</tbody></table></section>");

            html.AppendLine("<section><h2>Детализация событий</h2><table><thead><tr><th>Начало</th><th>Окончание / длительность</th><th>Оборудование</th><th>Группа</th><th>Тип</th><th>Ответственный</th><th>Описание</th></tr></thead><tbody>");
            foreach (var item in reportRows.OrderByDescending(x => x.Start).Take(300))
            {
                var equipment = string.IsNullOrWhiteSpace(item.InventoryNumber)
                    ? item.EquipmentTitle
                    : $"{item.EquipmentTitle} ({item.InventoryNumber})";

                var endOrDurationText = item.IsInProgress
                    ? TimeSpan.FromMinutes(Math.Max(0, (DateTime.Now - item.Start).TotalMinutes)).ToString(@"hh\:mm")
                    : item.End.ToString("dd.MM.yyyy HH:mm");

                var subdivisionText = string.IsNullOrWhiteSpace(item.Subdivision) ? "-" : item.Subdivision;
                html.AppendLine($"<tr><td>{item.Start:dd.MM.yyyy HH:mm}</td><td>{H(endOrDurationText)}</td><td>{H(equipment)}</td><td>{H(subdivisionText)}</td><td>{H(item.Type.ToString())}</td><td>{H(item.Responsible)}</td><td>{H(item.Description)}</td></tr>");
            }
            if (reportRows.Count == 0)
                html.AppendLine("<tr><td colspan=\"7\">Нет событий в выбранном периоде.</td></tr>");
            html.AppendLine("</tbody></table></section>");

            html.AppendLine("</body></html>");

            var outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EquipmentFailureAnalysis", "reports");
            Directory.CreateDirectory(outputDir);
            var reportPath = Path.Combine(outputDir, $"dea_report_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            File.WriteAllText(reportPath, html.ToString(), Encoding.UTF8);

            if (outputPathBox != null)
                outputPathBox.Text = reportPath;

            if (openAfterGenerate)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = reportPath,
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // ignore if shell open is unavailable
                }
            }

            await ShowMessageAsync("Отчеты", $"HTML-отчет сформирован: {reportPath}");
        }

        private void EmployeeAnalysisButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentPage = AppPage.EmployeeAnalysis;
            UpdatePageVisibility();
        }

        private void EmployeeTimelinePrevDate_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            var current = vm.EmployeeTimelineDate ?? DateTime.Now.Date;
            vm.EmployeeTimelineDate = current.AddDays(-1);
        }

        private void EmployeeTimelineNextDate_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            var current = vm.EmployeeTimelineDate ?? DateTime.Now.Date;
            vm.EmployeeTimelineDate = current.AddDays(1);
        }

        private void DowntimeEquipmentButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control control)
                return;

            if (control.DataContext is not EquipmentFailureAnalysis.Models.DowntimeEquipmentRow row || row.Equipment == null)
                return;

            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            vm.LoadEquipmentCommand.Execute(row.Equipment).Subscribe();
            vm.ShowDayTimelineCommand?.Execute(vm.DowntimeAnalysisDate).Subscribe();
            vm.SearchQuery = row.Equipment.Title ?? string.Empty;

            _currentPage = AppPage.FailureAnalysis;
            UpdatePageVisibility();
        }

        private void UpdatePageVisibility()
        {
            var isDashboardPage = _currentPage == AppPage.Dashboard;
            var isFailureAnalysisPage = _currentPage == AppPage.FailureAnalysis;
            var isDowntimeAnalysisPage = _currentPage == AppPage.DowntimeAnalysis;
            var isReportsPage = _currentPage == AppPage.Reports;
            var isSettingsPage = _currentPage == AppPage.Settings;
            var isEmployeeAnalysisPage = _currentPage == AppPage.EmployeeAnalysis;

            var dashboardPage = this.FindControl<Control>("DashboardPage");
            if (dashboardPage != null)
                dashboardPage.IsVisible = isDashboardPage;

            var failureCenterColumn = this.FindControl<Control>("FailureAnalysisCenterColumn");
            if (failureCenterColumn != null)
                failureCenterColumn.IsVisible = isFailureAnalysisPage;

            var failureRightColumn = this.FindControl<Control>("FailureAnalysisRightColumn");
            if (failureRightColumn != null)
                failureRightColumn.IsVisible = isFailureAnalysisPage;

            var downtimeAnalysisPage = this.FindControl<Control>("DowntimeAnalysisPage");
            if (downtimeAnalysisPage != null)
                downtimeAnalysisPage.IsVisible = isDowntimeAnalysisPage;

            var reportsPage = this.FindControl<Control>("ReportsPage");
            if (reportsPage != null)
                reportsPage.IsVisible = isReportsPage;

            var settingsPage = this.FindControl<Control>("SettingsPage");
            if (settingsPage != null)
                settingsPage.IsVisible = isSettingsPage;

            var employeeAnalysisPage = this.FindControl<Control>("EmployeeAnalysisPage");
            if (employeeAnalysisPage != null)
                employeeAnalysisPage.IsVisible = isEmployeeAnalysisPage;
        }

        private void EquipmentSearchBox_GotFocus(object? sender, GotFocusEventArgs e)
        {
            OpenEquipmentContextMenu(sender as Control);
        }

        private void EquipmentSearchBox_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            OpenEquipmentContextMenu(sender as Control);
        }

        private void FilterButton_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control target)
                return;

            if (target.GetVisualRoot() is null)
                return;

            if (this.DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            MenuItem CreateFilterItem(string title)
            {
                var item = new MenuItem
                {
                    Header = title,
                    ToggleType = MenuItemToggleType.Radio,
                    IsChecked = string.Equals(vm.SelectedIssueTypeFilter, title, StringComparison.Ordinal)
                };
                item.Click += (_, __) => vm.SelectedIssueTypeFilter = title;
                return item;
            }

            var menu = new ContextMenu
            {
                ItemsSource = new object[]
                {
                    CreateFilterItem("Все позиции"),
                    CreateFilterItem("Ремонты"),
                    CreateFilterItem("Настройки")
                }
            };

            menu.PlacementTarget = target;
            menu.Open(target);
        }

        private void OpenEquipmentContextMenu(Control? target)
        {
            if (target == null)
                return;

            if (target.GetVisualRoot() is null)
                return;

            if (this.DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            if (target.ContextMenu?.IsOpen == true)
                return;

            var now = DateTime.UtcNow;
            if ((now - _lastEquipmentMenuOpenUtc).TotalMilliseconds < 200)
                return;

            var equipment = vm.EquipmentCollection.ToList();
            if (equipment.Count == 0)
                return;

            var menuItems = equipment.Select(eq =>
            {
                var header = string.IsNullOrWhiteSpace(eq.InventoryNumber)
                    ? (eq.Title ?? string.Empty)
                    : $"{eq.Title} ({eq.InventoryNumber})";

                var item = new MenuItem { Header = header };
                item.Click += (_, __) =>
                {
                    vm.SelectedEquipmentFromSearch = eq;
                    vm.SearchQuery = eq.Title ?? string.Empty;
                };
                return item;
            }).ToList();

            var menu = new ContextMenu
            {
                ItemsSource = menuItems
            };

            _lastEquipmentMenuOpenUtc = now;
            menu.PlacementTarget = target;
            target.ContextMenu = menu;
            menu.Open(target);
        }

        private async void ImportButton_Click(object? sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filters.Add(new FileDialogFilter { Name = "XML files", Extensions = { "xml" } });
            dlg.AllowMultiple = true;
            var res = await dlg.ShowAsync(this);
            if (res == null || res.Length == 0)
                return;
            try
            {
                // allow importing and merging multiple XML files
                var decoder = new XmlDataDecoder(res);
                var items = decoder.DecodeEquipment();
                // pass to view model
                if (this.DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                {
                    vm.ImportEquipment(items);
                    // persist last imported file path
                    try { SaveLastImportedPath(res.First()); } catch { }
                }
            }
            catch (Exception ex)
            {
                var tb = new TextBlock { Text = "Ошибка при загрузке файла: " + ex.Message };
                var wnd = new Window
                {
                    Title = "Ошибка импорта",
                    Width = 400,
                    Height = 120,
                    Content = tb
                };
                await wnd.ShowDialog(this);
            }

        }

        private string GetLastImportedPathFile()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EquipmentFailureAnalysis");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "last_imported_xml.txt");
        }

        private void SaveLastImportedPath(string path)
        {
            var file = GetLastImportedPathFile();
            File.WriteAllText(file, path);
        }

        private string? LoadLastImportedPath()
        {
            var file = GetLastImportedPathFile();
            if (!File.Exists(file)) return null;
            try
            {
                var p = File.ReadAllText(file).Trim();
                return string.IsNullOrEmpty(p) ? null : p;
            }
            catch
            {
                return null;
            }
        }

        private void OnWindowResized()
        {
            // compute ideal DayCellSize so 31 columns fit into central heatmap viewport
            try
            {
                if (this.DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                {
                    // find the heatmap scroller control
                    var scroller = this.FindControl<ScrollViewer>("HeatmapScroller");
                    if (scroller != null)
                    {
                        // leave a larger margin so day cells don't overflow the visible area
                        double available = scroller.Bounds.Width - 120; // padding for labels/margins
                        if (available <= 0) return;
                        // 31 day columns
                        double cellWithMargin = available / 31.0;
                        // subtract extra to avoid overflow when including cell margins
                        double size = Math.Max(12.0, Math.Min(64.0, cellWithMargin - 6.0));
                        vm.DayCellSize = size;
                    }
                }
            }
            catch { }
        }
    }
}