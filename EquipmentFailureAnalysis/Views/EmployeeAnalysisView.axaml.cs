using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;

namespace EquipmentFailureAnalysis.Views
{
    public partial class EmployeeAnalysisView : UserControl
    {
        public EmployeeAnalysisView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void EmployeeTimelinePrevDate_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.EmployeeTimelinePrevDate_Click(sender, e);
        }

        private void EmployeeTimelineNextDate_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.EmployeeTimelineNextDate_Click(sender, e);
        }

        private void EmployeeAnalysisRow_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var tabs = this.FindControl<TabControl>("EmployeeAnalysisTabs");
            if (tabs != null)
            {
                tabs.SelectedIndex = 1;
            }

            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.EmployeeAnalysisRow_PointerPressed(sender, e);
        }

        private void EmployeeSetToday_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.SetEmployeeTimelineDate(DateTime.Today);
        }

        private void EmployeeEventRow_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control control)
                return;

            if (control.DataContext is not Models.Annotation annotation)
                return;

            if (DataContext is ViewModels.MainWindowViewModel vm)
            {
                vm.SelectedTimelineAnnotation = annotation;
            }
        }

        private async void CopyJiraKeyButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainWindowViewModel vm && !string.IsNullOrWhiteSpace(vm.SelectedTimelineJiraKey) && vm.SelectedTimelineJiraKey != "-")
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(vm.SelectedTimelineJiraKey);
                    vm.StatusMessage = $"Ключ Jira скопирован: {vm.SelectedTimelineJiraKey}";
                }
            }
        }
    }
}
