using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
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
    public partial class MainWindow
    {
        private bool _isNavigationCollapsed;
        private const double ExpandedNavigationWidth = 320d;
        private const double CollapsedNavigationWidth = 70d;
        private bool _reportSettingsWatchersAttached;
        private bool _reportToolsInitScheduled;

        private void ToggleNavigationButton_Click(object? sender, RoutedEventArgs e)
        {
            _isNavigationCollapsed = !_isNavigationCollapsed;
            ApplyNavigationPanelState();
        }

        private void ApplyNavigationPanelState()
        {
            var navigationHost = FindNestedControl<Grid>("NavigationHostGrid");
            if (navigationHost != null)
                navigationHost.Width = _isNavigationCollapsed ? CollapsedNavigationWidth : ExpandedNavigationWidth;

            var isExpanded = !_isNavigationCollapsed;

            SetControlVisibility("NavigationHeaderPanel", isExpanded);
            SetControlVisibility("DashboardNavTextPanel", isExpanded);
            SetControlVisibility("DowntimeNavTextPanel", isExpanded);
            SetControlVisibility("EmployeeNavTextPanel", isExpanded);
            SetControlVisibility("ReportsNavTextPanel", isExpanded);
            SetControlVisibility("SettingsNavTextPanel", isExpanded);

            UpdateNavigationButtonLayout("DashboardNavButton", isExpanded);
            UpdateNavigationButtonLayout("DowntimeNavButton", isExpanded);
            UpdateNavigationButtonLayout("EmployeeNavButton", isExpanded);
            UpdateNavigationButtonLayout("ReportsNavButton", isExpanded);
            UpdateNavigationButtonLayout("SettingsNavButton", isExpanded);

            var toggleGlyph = FindNestedControl<TextBlock>("NavigationToggleGlyph");
            if (toggleGlyph != null)
                toggleGlyph.Text = _isNavigationCollapsed ? "\uE76C" : "\uE76B";

            UpdateNavigationToggleLayout(isExpanded);
        }

        private void UpdateNavigationButtonLayout(string buttonName, bool isExpanded)
        {
            var button = FindNestedControl<Button>(buttonName);
            if (button == null)
                return;

            button.HorizontalContentAlignment = isExpanded ? HorizontalAlignment.Left : HorizontalAlignment.Center;
            button.Padding = isExpanded ? new Thickness(10, 8) : new Thickness(0);
            button.Width = isExpanded ? double.NaN : 48;
            button.Height = isExpanded ? double.NaN : 48;
            button.Margin = isExpanded ? new Thickness(0) : new Thickness(0, 2, 0, 2);

            if (isExpanded)
            {
                button.Classes.Remove("navCompact");
            }
            else if (!button.Classes.Contains("navCompact"))
            {
                button.Classes.Add("navCompact");
            }

            if (button.Content is Grid contentGrid)
            {
                contentGrid.Width = isExpanded ? double.NaN : 28d;
                contentGrid.HorizontalAlignment = isExpanded ? HorizontalAlignment.Left : HorizontalAlignment.Center;
            }
        }

        private void SetControlVisibility(string controlName, bool isVisible)
        {
            var control = FindNestedControl<Control>(controlName);
            if (control != null)
                control.IsVisible = isVisible;
        }

        private void UpdateNavigationToggleLayout(bool isExpanded)
        {
            var toggleButton = FindNestedControl<Button>("NavigationToggleButton");
            if (toggleButton == null)
                return;

            toggleButton.HorizontalAlignment = HorizontalAlignment.Right;
            toggleButton.VerticalAlignment = VerticalAlignment.Center;
        }

        private void SetNavigationButtonSelected(string buttonName, bool isSelected)
        {
            var button = FindNestedControl<Button>(buttonName);
            if (button == null)
                return;

            if (isSelected)
            {
                if (!button.Classes.Contains("selected"))
                    button.Classes.Add("selected");
            }
            else
            {
                button.Classes.Remove("selected");
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
            InitializeReportTools();
        }

        private void InitializeReportTools()
        {
            var startPicker = FindNestedControl<CalendarDatePicker>("ReportStartDatePicker");
            var endPicker = FindNestedControl<CalendarDatePicker>("ReportEndDatePicker");
            if (startPicker == null || endPicker == null)
            {
                ScheduleReportToolsInitialization();
                return;
            }

            if (startPicker != null && startPicker.SelectedDate == null)
                startPicker.SelectedDate = DateTime.Now.Date;

            if (endPicker != null && endPicker.SelectedDate == null)
                endPicker.SelectedDate = DateTime.Now.Date;

            AttachReportSettingsWatchers();
        }

        private void ScheduleReportToolsInitialization()
        {
            if (_reportToolsInitScheduled)
                return;

            _reportToolsInitScheduled = true;
            Dispatcher.UIThread.Post(() =>
            {
                _reportToolsInitScheduled = false;
                InitializeReportTools();
            }, DispatcherPriority.Loaded);
        }

        private void AttachReportSettingsWatchers()
        {
            if (_reportSettingsWatchersAttached)
                return;

            var attachedAny = false;
            var allCriticalFound = true;

            var startPicker = FindNestedControl<CalendarDatePicker>("ReportStartDatePicker");
            if (startPicker != null)
            {
                startPicker.PropertyChanged += (_, e) =>
                {
                    if (e.Property == CalendarDatePicker.SelectedDateProperty)
                        SaveJiraSettingsFromUi();
                };
                attachedAny = true;
            }
            else
            {
                allCriticalFound = false;
            }

            var endPicker = FindNestedControl<CalendarDatePicker>("ReportEndDatePicker");
            if (endPicker != null)
            {
                endPicker.PropertyChanged += (_, e) =>
                {
                    if (e.Property == CalendarDatePicker.SelectedDateProperty)
                        SaveJiraSettingsFromUi();
                };
                attachedAny = true;
            }
            else
            {
                allCriticalFound = false;
            }

            var groupByCombo = FindNestedControl<ComboBox>("ReportGroupByCombo");
            if (groupByCombo != null)
            {
                groupByCombo.PropertyChanged += (_, e) =>
                {
                    if (e.Property == ComboBox.SelectedItemProperty)
                        SaveJiraSettingsFromUi();
                };
                attachedAny = true;
            }
            else
            {
                allCriticalFound = false;
            }

            var checkBoxes = new[]
            {
                "ReportIncludeDashboardCheckBox",
                "ReportIncludeDowntimeCheckBox",
                "ReportIncludeEmployeeCheckBox",
                "ReportOpenAfterGenerateCheckBox",
                "ReportOnlyInProgressCheckBox",
                "ReportFilterByDurationCheckBox",
                "ReportFieldStartCheckBox",
                "ReportFieldEndCheckBox",
                "ReportFieldEquipmentCheckBox",
                "ReportFieldSubdivisionCheckBox",
                "ReportFieldTypeCheckBox",
                "ReportFieldResponsibleCheckBox",
                "ReportFieldDescriptionCheckBox"
            };

            foreach (var name in checkBoxes)
            {
                var checkBox = FindNestedControl<CheckBox>(name);
                if (checkBox == null)
                    continue;

                checkBox.PropertyChanged += (_, e) =>
                {
                    if (e.Property == CheckBox.IsCheckedProperty)
                        SaveJiraSettingsFromUi();
                };
                attachedAny = true;
            }

            var minDuration = FindNestedControl<NumericUpDown>("ReportMinDurationMinutesUpDown");
            if (minDuration != null)
            {
                minDuration.PropertyChanged += (_, e) =>
                {
                    if (e.Property == NumericUpDown.ValueProperty)
                        SaveJiraSettingsFromUi();
                };
                attachedAny = true;
            }
            else
            {
                allCriticalFound = false;
            }

            if (attachedAny && allCriticalFound)
            {
                _reportSettingsWatchersAttached = true;
                return;
            }

            ScheduleReportToolsInitialization();
        }

        internal async void GenerateHtmlReportButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            var startPicker = FindNestedControl<CalendarDatePicker>("ReportStartDatePicker");
            var endPicker = FindNestedControl<CalendarDatePicker>("ReportEndDatePicker");
            var includeDashboard = FindNestedControl<CheckBox>("ReportIncludeDashboardCheckBox")?.IsChecked == true;
            var includeDowntime = FindNestedControl<CheckBox>("ReportIncludeDowntimeCheckBox")?.IsChecked == true;
            var includeEmployee = FindNestedControl<CheckBox>("ReportIncludeEmployeeCheckBox")?.IsChecked == true;
            var openAfterGenerate = FindNestedControl<CheckBox>("ReportOpenAfterGenerateCheckBox")?.IsChecked == true;
            var onlyInProgress = FindNestedControl<CheckBox>("ReportOnlyInProgressCheckBox")?.IsChecked == true;
            var filterByDuration = FindNestedControl<CheckBox>("ReportFilterByDurationCheckBox")?.IsChecked == true;
            var minDurationMinutes = (double)(FindNestedControl<NumericUpDown>("ReportMinDurationMinutesUpDown")?.Value ?? 60m);
            var showStart = FindNestedControl<CheckBox>("ReportFieldStartCheckBox")?.IsChecked != false;
            var showEnd = FindNestedControl<CheckBox>("ReportFieldEndCheckBox")?.IsChecked != false;
            var showEquipment = FindNestedControl<CheckBox>("ReportFieldEquipmentCheckBox")?.IsChecked != false;
            var showSubdivision = FindNestedControl<CheckBox>("ReportFieldSubdivisionCheckBox")?.IsChecked != false;
            var showType = FindNestedControl<CheckBox>("ReportFieldTypeCheckBox")?.IsChecked != false;
            var showResponsible = FindNestedControl<CheckBox>("ReportFieldResponsibleCheckBox")?.IsChecked != false;
            var showDescription = FindNestedControl<CheckBox>("ReportFieldDescriptionCheckBox")?.IsChecked != false;
            var outputPathBox = FindNestedControl<TextBox>("ReportLastFilePathBox");

            SaveJiraSettingsFromUi();

            var startDate = startPicker?.SelectedDate?.Date ?? DateTime.Now.Date.AddDays(-30);
            var endDate = endPicker?.SelectedDate?.Date ?? DateTime.Now.Date;
            var groupBy = GetReportGroupByKeyFromUi();

            var options = new HtmlReportOptions
            {
                StartDate = startDate,
                EndDate = endDate,
                GroupBy = groupBy,
                IncludeDashboard = includeDashboard,
                IncludeDowntime = includeDowntime,
                IncludeEmployee = includeEmployee,
                OnlyInProgress = onlyInProgress,
                FilterByDuration = filterByDuration,
                MinDurationMinutes = minDurationMinutes,
                ShowStart = showStart,
                ShowEnd = showEnd,
                ShowEquipment = showEquipment,
                ShowSubdivision = showSubdivision,
                ShowType = showType,
                ShowResponsible = showResponsible,
                ShowDescription = showDescription
            };

            var generation = _htmlReportService.GenerateHtmlReport(vm, options);
            if (!generation.Success)
            {
                await ShowMessageAsync("Отчеты", generation.ErrorMessage);
                PublishStatus($"Ошибка формирования отчета: {generation.ErrorMessage}");
                return;
            }

            var reportPath = generation.ReportPath;

            if (outputPathBox != null)
                outputPathBox.Text = reportPath;

            SaveJiraSettingsFromUi();

            PublishStatus($"Отчет сформирован: {reportPath}");

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

        }

        private void EmployeeAnalysisButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentPage = AppPage.EmployeeAnalysis;
            UpdatePageVisibility();
        }

        internal void FailureHeatmapSettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            vm.SelectFailureHeatmapSettings();
        }

        internal void DowntimeHeatmapSettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            vm.SelectDowntimeHeatmapSettings();
        }

        internal void EmployeeTimelinePrevDate_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            var current = vm.EmployeeTimelineDate ?? DateTime.Now.Date;
            vm.EmployeeTimelineDate = current.AddDays(-1);
        }

        internal void EmployeeTimelineNextDate_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            var current = vm.EmployeeTimelineDate ?? DateTime.Now.Date;
            vm.EmployeeTimelineDate = current.AddDays(1);
        }

        internal void EmployeeAnalysisRow_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control control)
                return;

            if (control.DataContext is not EquipmentFailureAnalysis.Models.EmployeeAnalysisRow row)
                return;

            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            if (!string.IsNullOrWhiteSpace(row.Name))
                vm.SelectedEmployeeTimelineEmployee = row.Name;

            if (row.LastIssueDate != DateTime.MinValue)
                vm.EmployeeTimelineDate = row.LastIssueDate.Date;
        }

        internal void DowntimeEquipmentButton_Click(object? sender, PointerPressedEventArgs e)
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

            var dashboardPage = FindNestedControl<Control>("DashboardPage");
            if (dashboardPage != null)
                dashboardPage.IsVisible = isDashboardPage;

            var failureAnalysisPage = FindNestedControl<Control>("FailureAnalysisPage");
            if (failureAnalysisPage != null)
                failureAnalysisPage.IsVisible = isFailureAnalysisPage;

            var downtimeAnalysisPage = FindNestedControl<Control>("DowntimeAnalysisPage");
            if (downtimeAnalysisPage != null)
                downtimeAnalysisPage.IsVisible = isDowntimeAnalysisPage;

            var reportsPage = FindNestedControl<Control>("ReportsPage");
            if (reportsPage != null)
                reportsPage.IsVisible = isReportsPage;

            var settingsPage = FindNestedControl<Control>("SettingsPage");
            if (settingsPage != null)
                settingsPage.IsVisible = isSettingsPage;

            var employeeAnalysisPage = FindNestedControl<Control>("EmployeeAnalysisPage");
            if (employeeAnalysisPage != null)
                employeeAnalysisPage.IsVisible = isEmployeeAnalysisPage;

            SetNavigationButtonSelected("DashboardNavButton", isDashboardPage);
            SetNavigationButtonSelected("DowntimeNavButton", isDowntimeAnalysisPage || isFailureAnalysisPage);
            SetNavigationButtonSelected("EmployeeNavButton", isEmployeeAnalysisPage);
            SetNavigationButtonSelected("ReportsNavButton", isReportsPage);
            SetNavigationButtonSelected("SettingsNavButton", isSettingsPage);
        }

    }
}
