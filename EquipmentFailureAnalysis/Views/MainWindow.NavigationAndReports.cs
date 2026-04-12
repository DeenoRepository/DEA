using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
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
    public partial class MainWindow
    {
        private bool _isNavigationCollapsed;
        private const double ExpandedNavigationWidth = 320d;
        private const double CollapsedNavigationWidth = 70d;

        private void ToggleNavigationButton_Click(object? sender, RoutedEventArgs e)
        {
            _isNavigationCollapsed = !_isNavigationCollapsed;
            ApplyNavigationPanelState();
        }

        private void ApplyNavigationPanelState()
        {
            var navigationHost = this.FindControl<Grid>("NavigationHostGrid");
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

            var toggleGlyph = this.FindControl<TextBlock>("NavigationToggleGlyph");
            if (toggleGlyph != null)
                toggleGlyph.Text = _isNavigationCollapsed ? "\uE76C" : "\uE76B";

            UpdateNavigationToggleLayout(isExpanded);
        }

        private void UpdateNavigationButtonLayout(string buttonName, bool isExpanded)
        {
            var button = this.FindControl<Button>(buttonName);
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
            var control = this.FindControl<Control>(controlName);
            if (control != null)
                control.IsVisible = isVisible;
        }

        private void UpdateNavigationToggleLayout(bool isExpanded)
        {
            var toggleButton = this.FindControl<Button>("NavigationToggleButton");
            if (toggleButton == null)
                return;

            toggleButton.HorizontalAlignment = HorizontalAlignment.Right;
            toggleButton.VerticalAlignment = VerticalAlignment.Center;
        }

        private void SetNavigationButtonSelected(string buttonName, bool isSelected)
        {
            var button = this.FindControl<Button>(buttonName);
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
            var filterByDuration = this.FindControl<CheckBox>("ReportFilterByDurationCheckBox")?.IsChecked == true;
            var minDurationMinutes = (double)(this.FindControl<NumericUpDown>("ReportMinDurationMinutesUpDown")?.Value ?? 60m);
            var showStart = this.FindControl<CheckBox>("ReportFieldStartCheckBox")?.IsChecked != false;
            var showEnd = this.FindControl<CheckBox>("ReportFieldEndCheckBox")?.IsChecked != false;
            var showEquipment = this.FindControl<CheckBox>("ReportFieldEquipmentCheckBox")?.IsChecked != false;
            var showSubdivision = this.FindControl<CheckBox>("ReportFieldSubdivisionCheckBox")?.IsChecked != false;
            var showType = this.FindControl<CheckBox>("ReportFieldTypeCheckBox")?.IsChecked != false;
            var showResponsible = this.FindControl<CheckBox>("ReportFieldResponsibleCheckBox")?.IsChecked != false;
            var showDescription = this.FindControl<CheckBox>("ReportFieldDescriptionCheckBox")?.IsChecked != false;
            var outputPathBox = this.FindControl<TextBox>("ReportLastFilePathBox");

            SaveJiraSettingsFromUi();

            var startDate = startPicker?.SelectedDate?.Date ?? DateTime.Now.Date.AddDays(-30);
            var endDate = endPicker?.SelectedDate?.Date ?? DateTime.Now.Date;
            string groupBy = "По дням";
            if (groupByCombo?.SelectedItem is ComboBoxItem selectedGroup)
                groupBy = selectedGroup.Content?.ToString() ?? groupBy;

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

        private void FailureHeatmapSettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            var failureOption = vm.HeatmapSettingOptions
                .FirstOrDefault(v => v.Contains("РЅРµРёСЃРїСЂР°РІ", StringComparison.CurrentCultureIgnoreCase));

            if (!string.IsNullOrWhiteSpace(failureOption))
                vm.SelectedHeatmapSetting = failureOption;
        }

        private void DowntimeHeatmapSettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            var downtimeOption = vm.HeatmapSettingOptions
                .FirstOrDefault(v => v.Contains("РїСЂРѕСЃС‚Рѕ", StringComparison.CurrentCultureIgnoreCase));

            if (!string.IsNullOrWhiteSpace(downtimeOption))
                vm.SelectedHeatmapSetting = downtimeOption;
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

        private void EmployeeAnalysisRow_PointerPressed(object? sender, PointerPressedEventArgs e)
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

            SetNavigationButtonSelected("DashboardNavButton", isDashboardPage);
            SetNavigationButtonSelected("DowntimeNavButton", isDowntimeAnalysisPage || isFailureAnalysisPage);
            SetNavigationButtonSelected("EmployeeNavButton", isEmployeeAnalysisPage);
            SetNavigationButtonSelected("ReportsNavButton", isReportsPage);
            SetNavigationButtonSelected("SettingsNavButton", isSettingsPage);
        }

    }
}
