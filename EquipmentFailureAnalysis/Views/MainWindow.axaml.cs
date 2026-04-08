using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.VisualTree;
using EquipmentFailureAnalysis.Utility;
using System.Linq;
using System;
using System.IO;

namespace EquipmentFailureAnalysis.Views
{
    public partial class MainWindow : Window
    {
        private DateTime _lastEquipmentMenuOpenUtc = DateTime.MinValue;
        private AppPage _currentPage = AppPage.FailureAnalysis;

        private enum AppPage
        {
            FailureAnalysis,
            DowntimeAnalysis,
            Settings
        }

        public MainWindow()
        {
            InitializeComponent();
            this.GetObservable<Rect>(BoundsProperty).Subscribe(_ => OnWindowResized());
            UpdatePageVisibility();
        }

        private void FailureAnalysisButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentPage = AppPage.FailureAnalysis;
            UpdatePageVisibility();
        }

        private void DowntimeAnalysisButton_Click(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
            {
                vm.ShowDowntimeDayCommand.Execute(DateTime.Now.Date).Subscribe();
            }

            _currentPage = AppPage.DowntimeAnalysis;
            UpdatePageVisibility();
        }

        private void SettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            _currentPage = AppPage.Settings;
            UpdatePageVisibility();
        }

        private void UpdatePageVisibility()
        {
            var isFailureAnalysisPage = _currentPage == AppPage.FailureAnalysis;
            var isDowntimeAnalysisPage = _currentPage == AppPage.DowntimeAnalysis;
            var isSettingsPage = _currentPage == AppPage.Settings;

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