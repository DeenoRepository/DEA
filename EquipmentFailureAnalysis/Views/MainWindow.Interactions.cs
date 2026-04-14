using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
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
    public partial class MainWindow
    {
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

        internal async void ImportButton_Click(object? sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Импорт XML",
                AllowMultiple = true,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("XML files")
                    {
                        Patterns = new List<string> { "*.xml" }
                    }
                }
            });

            var res = (files ?? new List<IStorageFile>())
                .Select(file => file.TryGetLocalPath())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path!)
                .ToArray();

            if (res.Length == 0)
                return;

            var result = _xmlImportService.ImportFromPaths(res);
            if (!result.Success)
            {
                var tb = new TextBlock { Text = "Ошибка при загрузке файла: " + result.ErrorMessage };
                var wnd = new Window
                {
                    Title = "Ошибка импорта",
                    Width = 400,
                    Height = 120,
                    Content = tb
                };
                await wnd.ShowDialog(this);
                PublishStatus($"Ошибка импорта XML: {result.ErrorMessage}");
                return;
            }

            if (this.DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                vm.ImportEquipment(result.Items);

            PublishStatus($"Импорт XML завершен: {result.Items.Count} ед. оборудования.");
        }

        private void OnWindowResized()
        {
            // Compute DayCellSize so 31 columns fill current page heatmap viewport.
            try
            {
                if (this.DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                {
                    ScrollViewer? targetScroller = _currentPage switch
                    {
                        AppPage.FailureAnalysis => FindNestedControl<ScrollViewer>("FailureHeatmapScroller"),
                        AppPage.DowntimeAnalysis => FindNestedControl<ScrollViewer>("DowntimeHeatmapScroller"),
                        _ => null
                    };

                    if (targetScroller == null || targetScroller.Bounds.Width <= 0)
                    {
                        targetScroller = new[]
                        {
                            FindNestedControl<ScrollViewer>("FailureHeatmapScroller"),
                            FindNestedControl<ScrollViewer>("DowntimeHeatmapScroller"),
                            FindNestedControl<ScrollViewer>("HeatmapScroller")
                        }
                        .FirstOrDefault(s => s != null && s.IsEffectivelyVisible && s.Bounds.Width > 0);
                    }

                    if (targetScroller == null)
                        return;

                    // 72px month label + margins of row containers/text blocks.
                    const double fixedRowOverhead = 80.0;
                    double available = targetScroller.Bounds.Width - fixedRowOverhead;
                    if (available <= 0)
                        return;

                    double cellWithMargin = available / 31.0;
                    // Per-cell external margins are ~4px in the row templates.
                    double size = Math.Max(12.0, Math.Min(120.0, cellWithMargin - 4.0));
                    vm.DayCellSize = size;
                }
            }
            catch { }
        }
    }
}

