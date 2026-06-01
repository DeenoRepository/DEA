using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;

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
    }
}
