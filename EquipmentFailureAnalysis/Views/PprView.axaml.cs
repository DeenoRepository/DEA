using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.ViewModels;
using System;
using System.Collections.Generic;

namespace EquipmentFailureAnalysis.Views
{
    public partial class PprView : UserControl
    {
        public PprView()
        {
            InitializeComponent();
        }

        private async void ImportExcel_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Выберите файл графика ППР",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Excel Таблицы (*.xlsx)")
                    {
                        Patterns = new[] { "*.xlsx" }
                    }
                }
            });

            if (files != null && files.Count > 0)
            {
                var localPath = files[0].Path.LocalPath;
                if (DataContext is PprViewModel vm)
                {
                    vm.ImportFromExcelCommand.Execute(localPath).Subscribe();
                }
            }
        }

        private async void ExportExcel_Click(object? sender, RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Экспорт графика ППР в Excel",
                SuggestedFileName = $"ppr_schedule_{DateTime.Now:yyyyMMdd_HHmmss}",
                DefaultExtension = "xlsx",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Excel Таблицы (*.xlsx)")
                    {
                        Patterns = new[] { "*.xlsx" }
                    }
                }
            });

            if (file != null)
            {
                var localPath = file.Path.LocalPath;
                if (DataContext is PprViewModel vm)
                {
                    vm.ExportToExcelCommand.Execute(localPath).Subscribe();
                }
            }
        }

        private void MonthCell_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PprScheduleItem item && btn.Tag is string tagStr && int.TryParse(tagStr, out int m))
            {
                if (DataContext is PprViewModel vm)
                {
                    vm.ToggleCompletionCommand.Execute(new PprViewModel.ToggleCompletionArgs { Item = item, MonthIndex = m }).Subscribe();
                }
            }
        }
    }
}
