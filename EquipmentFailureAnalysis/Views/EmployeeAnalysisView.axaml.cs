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
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.EmployeeAnalysisRow_PointerPressed(sender, e);
        }

        private void EmployeeSetToday_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.SetEmployeeTimelineDate(DateTime.Today);
        }
    }
}
