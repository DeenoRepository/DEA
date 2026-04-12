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
                    CreateFilterItem("Р’СЃРµ РїРѕР·РёС†РёРё"),
                    CreateFilterItem("Р РµРјРѕРЅС‚С‹"),
                    CreateFilterItem("РќР°СЃС‚СЂРѕР№РєРё")
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
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "РРјРїРѕСЂС‚ XML",
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
                return;
            }

            if (this.DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                vm.ImportEquipment(result.Items);
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
