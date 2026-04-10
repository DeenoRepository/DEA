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
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EquipmentFailureAnalysis.Views
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<string> _jiraFilterIds = new ObservableCollection<string>();

        private DateTime _lastEquipmentMenuOpenUtc = DateTime.MinValue;
        private AppPage _currentPage = AppPage.Dashboard;

        private enum AppPage
        {
            Dashboard,
            FailureAnalysis,
            DowntimeAnalysis,
            Settings,
            EmployeeAnalysis
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
            var fromFilters = $"({filterClause}) AND status = 'Решен'";

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
            var list = this.FindControl<ListBox>("JiraFilterIdsList");
            if (list != null)
                list.ItemsSource = _jiraFilterIds;
            LoadJiraSettingsToUi();
            this.GetObservable<Rect>(BoundsProperty).Subscribe(_ => OnWindowResized());
            UpdatePageVisibility();
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
                    JiraFilterIds = GetFilterIdsFromUi().ToList()
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
            }
            catch
            {
                // ignore invalid settings file
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