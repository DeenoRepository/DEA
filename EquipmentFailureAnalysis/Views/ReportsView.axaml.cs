using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using System;

namespace EquipmentFailureAnalysis.Views
{
    public partial class ReportsView : UserControl
    {
        private bool _settingsObserversAttached;

        public ReportsView()
        {
            InitializeComponent();
            AttachedToVisualTree += ReportsView_AttachedToVisualTree;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ReportsView_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            AttachSettingsObservers();
        }

        private void AttachSettingsObservers()
        {
            if (_settingsObserversAttached)
                return;

            void Watch(Control? control, AvaloniaProperty property)
            {
                if (control == null)
                    return;

                control.PropertyChanged += (_, args) =>
                {
                    if (args.Property == property)
                        NotifyReportSettingsChanged();
                };
            }

            Watch(this.FindControl<CalendarDatePicker>("ReportStartDatePicker"), CalendarDatePicker.SelectedDateProperty);
            Watch(this.FindControl<CalendarDatePicker>("ReportEndDatePicker"), CalendarDatePicker.SelectedDateProperty);
            Watch(this.FindControl<ComboBox>("ReportGroupByCombo"), ComboBox.SelectedItemProperty);
            Watch(this.FindControl<NumericUpDown>("ReportMinDurationMinutesUpDown"), NumericUpDown.ValueProperty);

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
                Watch(this.FindControl<CheckBox>(name), ToggleButton.IsCheckedProperty);

            _settingsObserversAttached = true;
        }

        private void NotifyReportSettingsChanged()
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.ReportSettingsChanged();
        }

        private void GenerateHtmlReportButton_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.GenerateHtmlReportButton_Click(sender, e);
        }

        private void ExportPdfButton_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.ExportPdfButton_Click(sender, e);
        }

        private void ExportCsvButton_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.ExportCsvButton_Click(sender, e);
        }

        private void ExportDowntimeLossExcel_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.ExportDowntimeLossExcel_Click(sender, e);
        }

        private async void CopyPathButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ReportsViewModel vm && !string.IsNullOrWhiteSpace(vm.ReportLastFilePath))
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(vm.ReportLastFilePath);
                    if (topLevel is MainWindow host)
                    {
                        if (host.DataContext is ViewModels.MainWindowViewModel mainVm)
                        {
                            mainVm.StatusMessage = $"Путь к отчету скопирован: {vm.ReportLastFilePath}";
                        }
                    }
                }
            }
        }

        private void OpenFileButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ReportsViewModel vm && !string.IsNullOrWhiteSpace(vm.ReportLastFilePath))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = vm.ReportLastFilePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    if (TopLevel.GetTopLevel(this) is MainWindow host)
                    {
                        if (host.DataContext is ViewModels.MainWindowViewModel mainVm)
                        {
                            mainVm.StatusMessage = $"Не удалось открыть файл: {ex.Message}";
                        }
                    }
                }
            }
        }

        private void Preset1Year_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ReportsViewModel vm)
                vm.SetPeriodPreset("1year");
        }

        private void Preset2Years_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ReportsViewModel vm)
                vm.SetPeriodPreset("2years");
        }

        private void Preset3Years_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ReportsViewModel vm)
                vm.SetPeriodPreset("3years");
        }

        private void Preset5Years_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ReportsViewModel vm)
                vm.SetPeriodPreset("5years");
        }

        private void PresetAll_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ReportsViewModel vm)
                vm.SetPeriodPreset("all");
        }
    }
}
