using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
        private bool _isRightPanelCollapsed;
        private const double ExpandedNavigationWidth = 320d;
        private const double CollapsedNavigationWidth = 70d;
        private const double ExpandedRightPanelWidth = 440d;
        private string _savedDowntimeIssueTypeFilter = "Все типы";
        private string _savedDowntimeStatusFilter = "Все статусы";
        private string _savedDowntimeResponsibleFilter = "Все ответственные";
        private string _savedDowntimeSubdivisionFilter = "Все группы";
        private string _savedDowntimeEquipmentSearchQuery = string.Empty;
        private string _savedDashboardSubdivisionFilter = "Все группы";

        private void ToggleNavigationButton_Click(object? sender, RoutedEventArgs e)
        {
            _isNavigationCollapsed = !_isNavigationCollapsed;
            ApplyNavigationPanelState();
            ScheduleRightPanelToggleReposition();
        }

        private void ToggleRightPanelButton_Click(object? sender, RoutedEventArgs e)
        {
            _isRightPanelCollapsed = !_isRightPanelCollapsed;
            ApplyRightPanelState();
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
            UpdateRightPanelTogglePosition();
            ScheduleRightPanelToggleReposition();
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

        private void ApplyRightPanelState()
        {
            ApplyRightPanelStateForLayout("DashboardLayoutRoot", "DashboardRightColumn");
            ApplyRightPanelStateForLayout("FailureAnalysisLayoutRoot", "FailureAnalysisRightColumn");
            ApplyRightPanelStateForLayout("DowntimeLayoutRoot", "DowntimeRightColumn");
            ApplyRightPanelStateForLayout("ReportsLayoutRoot", "ReportsRightColumn");
            ApplyRightPanelStateForLayout("SettingsLayoutRoot", "SettingsRightColumn");
            ApplyRightPanelStateForLayout("EmployeeAnalysisLayoutRoot", "EmployeeAnalysisRightColumn");

            var glyph = FindNestedControl<TextBlock>("RightPanelToggleGlyph");
            if (glyph != null)
                glyph.Text = _isRightPanelCollapsed ? "\uE76B" : "\uE76C";

            var toggleButton = FindNestedControl<Button>("RightPanelToggleButton");
            if (toggleButton != null)
            {
                ToolTip.SetTip(toggleButton, _isRightPanelCollapsed
                    ? "Развернуть правую панель"
                    : "Свернуть правую панель");
            }

            UpdateRightPanelTogglePosition();
        }

        private void ApplyRightPanelStateForLayout(string layoutRootName, string rightColumnName)
        {
            var layoutRoot = FindNestedControl<Grid>(layoutRootName);
            if (layoutRoot == null || layoutRoot.ColumnDefinitions.Count < 2)
                return;

            var rightColumn = FindNestedControl<Control>(rightColumnName);
            if (rightColumn != null)
                rightColumn.IsVisible = !_isRightPanelCollapsed;

            layoutRoot.ColumnDefinitions[1].Width = _isRightPanelCollapsed
                ? new GridLength(0)
                : new GridLength(ExpandedRightPanelWidth, GridUnitType.Pixel);
        }

        private void UpdateRightPanelTogglePosition()
        {
            var toggleButton = FindNestedControl<Button>("RightPanelToggleButton");
            if (toggleButton == null)
                return;

            if (_isRightPanelCollapsed)
            {
                DockRightPanelToggleToWindowEdge(toggleButton);
                return;
            }

            var mainLayout = FindNestedControl<Grid>("MainLayoutGrid");
            if (mainLayout == null || mainLayout.ColumnDefinitions.Count < 4)
            {
                DockRightPanelToggleToWindowEdge(toggleButton);
                return;
            }

            var firstColumnWidth = mainLayout.ColumnDefinitions[0].ActualWidth;
            var secondColumnWidth = mainLayout.ColumnDefinitions[1].ActualWidth;
            var splitterColumnWidth = mainLayout.ColumnDefinitions[2].ActualWidth;
            // Right panels have a left margin of 8px; align toggle with the panel's actual left border.
            var anchorLeft = firstColumnWidth + secondColumnWidth + splitterColumnWidth + 8d;
            if (double.IsNaN(anchorLeft) || double.IsInfinity(anchorLeft) || anchorLeft <= 0)
            {
                DockRightPanelToggleToWindowEdge(toggleButton);
                return;
            }

            const double toggleWidth = 20d;
            var left = Math.Max(0, anchorLeft - (toggleWidth / 2d));
            if (double.IsNaN(left) || double.IsInfinity(left))
            {
                DockRightPanelToggleToWindowEdge(toggleButton);
                return;
            }

            toggleButton.HorizontalAlignment = HorizontalAlignment.Left;
            toggleButton.Margin = new Thickness(left, 0, 0, 0);
        }

        private void ScheduleRightPanelToggleReposition()
        {
            Dispatcher.UIThread.Post(
                UpdateRightPanelTogglePosition,
                DispatcherPriority.Loaded);
        }

        private static void DockRightPanelToggleToWindowEdge(Button toggleButton)
        {
            toggleButton.HorizontalAlignment = HorizontalAlignment.Right;
            toggleButton.Margin = new Thickness(0);
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
            CapturePageFilters();
            _currentPage = AppPage.FailureAnalysis;
            UpdatePageVisibility();
            RestorePageFilters();
        }

        private void DashboardButton_Click(object? sender, RoutedEventArgs e)
        {
            CapturePageFilters();
            _currentPage = AppPage.Dashboard;
            UpdatePageVisibility();
            RestorePageFilters();
        }

        private void DowntimeAnalysisButton_Click(object? sender, RoutedEventArgs e)
        {
            CapturePageFilters();
            if (this.DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
            {
                vm.Downtime.ShowDowntimeDayCommand.Execute(vm.Downtime.DowntimeAnalysisDate).Subscribe();
            }

            _currentPage = AppPage.DowntimeAnalysis;
            UpdatePageVisibility();
            RestorePageFilters();
        }

        private void SettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            CapturePageFilters();
            _currentPage = AppPage.Settings;
            UpdatePageVisibility();
            RestorePageFilters();
        }

        private void ReportsButton_Click(object? sender, RoutedEventArgs e)
        {
            CapturePageFilters();
            _currentPage = AppPage.Reports;
            UpdatePageVisibility();
            InitializeReportTools();
            RestorePageFilters();
        }

        private void InitializeReportTools()
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            vm.Reports.EnsureDefaultPeriod(DateTime.Now);
        }

        internal async void GenerateHtmlReportButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            var reports = vm.Reports;
            if (!reports.TryBuildHtmlReportOptions(DateTime.Now, out var options, out var validationError))
            {
                var message = validationError ?? "Параметры отчета заполнены некорректно.";
                await ShowMessageAsync("Отчеты", message);
                PublishStatus($"Ошибка формирования отчета: {message}");
                return;
            }

            var generation = _htmlReportService.GenerateHtmlReport(vm, options);
            if (!generation.Success)
            {
                await ShowMessageAsync("Отчеты", generation.ErrorMessage);
                PublishStatus($"Ошибка формирования отчета: {generation.ErrorMessage}");
                return;
            }

            var reportPath = generation.ReportPath;

            reports.ReportLastFilePath = reportPath;

            SaveJiraSettingsFromUi();

            PublishStatus($"Отчет сформирован: {reportPath}");

            if (reports.ReportOpenAfterGenerate)
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

        internal async void CaptureMiddleColumnScreenshotButton_Click(object? sender, RoutedEventArgs e)
        {
            var middleColumn = ResolveMiddleColumnTarget();
            if (middleColumn == null)
            {
                const string message = "Средняя колонка не найдена для текущей страницы.";
                await ShowMessageAsync("Скриншот", message);
                PublishStatus(message);
                return;
            }

            var renderTarget = ResolveRenderTargetForMiddleColumn(middleColumn);
            if (renderTarget.Bounds.Width <= 1 || renderTarget.Bounds.Height <= 1)
            {
                const string message = "Средняя колонка еще не готова к захвату. Попробуйте повторить через секунду.";
                await ShowMessageAsync("Скриншот", message);
                PublishStatus(message);
                return;
            }

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить скриншот средней колонки",
                SuggestedFileName = $"dea-middle-column-{DateTime.Now:yyyyMMdd-HHmmss}",
                DefaultExtension = "png",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new("PNG image")
                    {
                        Patterns = new List<string> { "*.png" },
                        MimeTypes = new List<string> { "image/png" }
                    }
                }
            });

            if (file == null)
                return;

            RenderTargetBitmap? finalBitmap = null;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                const double centerTopMarginDip = 12d;
                const double centerBottomMarginDip = 12d;
                const double centerRightMarginDip = 8d;
                const double rightLeftMarginDip = 8d;

                var scale = VisualRoot?.RenderScaling ?? 1d;
                var dpi = new Vector(96d * scale, 96d * scale);

                var centerPixelSize = new PixelSize(
                    Math.Max(1, (int)Math.Ceiling(renderTarget.Bounds.Width * scale)),
                    Math.Max(1, (int)Math.Ceiling(renderTarget.Bounds.Height * scale)));
                using var centerBitmap = new RenderTargetBitmap(centerPixelSize, dpi);
                centerBitmap.Render(renderTarget);

                var rightColumn = ResolveRightColumnTarget();
                var includeRightColumn = !_isRightPanelCollapsed
                    && rightColumn != null
                    && rightColumn.IsVisible
                    && rightColumn.Bounds.Width > 1
                    && rightColumn.Bounds.Height > 1;

                var centerWidthDip = renderTarget.Bounds.Width;
                var centerHeightDip = renderTarget.Bounds.Height;

                var totalWidthDip = centerWidthDip + centerRightMarginDip;
                var totalHeightDip = centerHeightDip + centerTopMarginDip + centerBottomMarginDip;

                var compositionRoot = new Grid
                {
                    Width = totalWidthDip,
                    Height = totalHeightDip,
                    Background = Brushes.Transparent,
                    IsHitTestVisible = false
                };
                compositionRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(totalWidthDip, GridUnitType.Pixel) });

                var centerImage = new Image
                {
                    Source = centerBitmap,
                    Stretch = Stretch.None,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, centerTopMarginDip, 0, centerBottomMarginDip)
                };
                Grid.SetColumn(centerImage, 0);

                compositionRoot.Children.Add(centerImage);

                RenderTargetBitmap? rightBitmap = null;
                if (includeRightColumn)
                {
                    var rightPixelSize = new PixelSize(
                        Math.Max(1, (int)Math.Ceiling(rightColumn!.Bounds.Width * scale)),
                        Math.Max(1, (int)Math.Ceiling(rightColumn.Bounds.Height * scale)));
                    rightBitmap = new RenderTargetBitmap(rightPixelSize, dpi);
                    rightBitmap.Render(rightColumn);

                    var rightWidthDip = rightColumn.Bounds.Width;
                    var rightHeightDip = rightColumn.Bounds.Height;

                    totalWidthDip = centerWidthDip + centerRightMarginDip + rightLeftMarginDip + rightWidthDip;
                    totalHeightDip = Math.Max(centerHeightDip + centerTopMarginDip + centerBottomMarginDip, rightHeightDip);
                    compositionRoot.Width = totalWidthDip;
                    compositionRoot.Height = totalHeightDip;
                    var rightX = centerWidthDip + centerRightMarginDip + rightLeftMarginDip;

                    // Extend right panel background to the full screenshot height when center content is taller.
                    var rightPanelBackground = new Border
                    {
                        Width = rightWidthDip,
                        Height = totalHeightDip,
                        Background = new SolidColorBrush(Color.Parse("#FFFFFF")),
                        BorderBrush = new SolidColorBrush(Color.Parse("#E1E1E1")),
                        BorderThickness = new Thickness(1, 0, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(rightX, 0, 0, 0)
                    };
                    compositionRoot.Children.Add(rightPanelBackground);

                    var rightImage = new Image
                    {
                        Source = rightBitmap,
                        Stretch = Stretch.None,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(rightX, 0, 0, 0)
                    };
                    compositionRoot.Children.Add(rightImage);
                }

                compositionRoot.Measure(new Size(totalWidthDip, totalHeightDip));
                compositionRoot.Arrange(new Rect(0, 0, totalWidthDip, totalHeightDip));

                var composedPixelSize = new PixelSize(
                    Math.Max(1, (int)Math.Ceiling(totalWidthDip * scale)),
                    Math.Max(1, (int)Math.Ceiling(totalHeightDip * scale)));
                finalBitmap = new RenderTargetBitmap(composedPixelSize, dpi);
                finalBitmap.Render(compositionRoot);
                rightBitmap?.Dispose();
            }, DispatcherPriority.Render);

            if (finalBitmap == null)
                return;

            await using (var stream = await file.OpenWriteAsync())
            {
                finalBitmap.Save(stream);
            }
            finalBitmap.Dispose();

            PublishStatus($"Скриншот средней колонки сохранен: {file.Path.LocalPath}");
        }

        private Control? ResolveRightColumnTarget()
        {
            var targetName = _currentPage switch
            {
                AppPage.Dashboard => "DashboardRightColumn",
                AppPage.FailureAnalysis => "FailureAnalysisRightColumn",
                AppPage.DowntimeAnalysis => "DowntimeRightColumn",
                AppPage.Reports => "ReportsRightColumn",
                AppPage.Settings => "SettingsRightColumn",
                AppPage.EmployeeAnalysis => "EmployeeAnalysisRightColumn",
                _ => null
            };

            if (string.IsNullOrWhiteSpace(targetName))
                return null;

            return FindNestedControl<Control>(targetName);
        }

        private Control? ResolveActiveLayoutRoot()
        {
            var layoutName = _currentPage switch
            {
                AppPage.Dashboard => "DashboardLayoutRoot",
                AppPage.FailureAnalysis => "FailureAnalysisLayoutRoot",
                AppPage.DowntimeAnalysis => "DowntimeLayoutRoot",
                AppPage.Reports => "ReportsLayoutRoot",
                AppPage.Settings => "SettingsLayoutRoot",
                AppPage.EmployeeAnalysis => "EmployeeAnalysisLayoutRoot",
                _ => null
            };

            if (string.IsNullOrWhiteSpace(layoutName))
                return null;

            return FindNestedControl<Control>(layoutName);
        }

        private static Control ResolveRenderTargetForMiddleColumn(Control middleColumn)
        {
            if (middleColumn is ScrollViewer scrollViewer && scrollViewer.Content is Control contentControl)
                return contentControl;

            return middleColumn;
        }

        private Control? ResolveMiddleColumnTarget()
        {
            var targetName = _currentPage switch
            {
                AppPage.Dashboard => "DashboardCenterColumn",
                AppPage.FailureAnalysis => "FailureAnalysisCenterColumn",
                AppPage.DowntimeAnalysis => "DowntimeCenterColumn",
                AppPage.Reports => "ReportsCenterColumn",
                AppPage.Settings => "SettingsCenterColumn",
                AppPage.EmployeeAnalysis => "EmployeeCenterColumn",
                _ => null
            };

            if (string.IsNullOrWhiteSpace(targetName))
                return null;

            return FindNestedControl<Control>(targetName);
        }

        private void EmployeeAnalysisButton_Click(object? sender, RoutedEventArgs e)
        {
            CapturePageFilters();
            _currentPage = AppPage.EmployeeAnalysis;
            UpdatePageVisibility();
            RestorePageFilters();
        }

        private void CapturePageFilters()
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            _savedDowntimeIssueTypeFilter = vm.SelectedDowntimeIssueTypeFilter;
            _savedDowntimeStatusFilter = vm.SelectedDowntimeStatusFilter;
            _savedDowntimeResponsibleFilter = vm.SelectedDowntimeResponsibleFilter;
            _savedDowntimeSubdivisionFilter = vm.SelectedDowntimeSubdivisionFilter;
            _savedDowntimeEquipmentSearchQuery = vm.DowntimeEquipmentSearchQuery;
            _savedDashboardSubdivisionFilter = vm.Dashboard.SelectedDashboardSubdivisionFilter;
        }

        private void RestorePageFilters()
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            vm.SelectedDowntimeIssueTypeFilter = _savedDowntimeIssueTypeFilter;
            vm.SelectedDowntimeStatusFilter = _savedDowntimeStatusFilter;
            vm.SelectedDowntimeResponsibleFilter = _savedDowntimeResponsibleFilter;
            vm.SelectedDowntimeSubdivisionFilter = _savedDowntimeSubdivisionFilter;
            vm.DowntimeEquipmentSearchQuery = _savedDowntimeEquipmentSearchQuery;
            vm.Dashboard.SelectedDashboardSubdivisionFilter = _savedDashboardSubdivisionFilter;
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
            vm.ShowDayTimelineCommand?.Execute(vm.Downtime.DowntimeAnalysisDate).Subscribe();
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

            // Recalculate heatmap cell size when page visibility changes.
            // Do it immediately and once more after layout pass so Bounds are actual.
            ApplyRightPanelState();
            OnWindowResized();
            Avalonia.Threading.Dispatcher.UIThread.Post(
                OnWindowResized,
                Avalonia.Threading.DispatcherPriority.Loaded);
        }

    }
}

