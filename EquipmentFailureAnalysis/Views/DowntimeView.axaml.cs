using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;

namespace EquipmentFailureAnalysis.Views
{
    public partial class DowntimeView : UserControl
    {
        public DowntimeView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void DowntimeHeatmapSettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.DowntimeHeatmapSettingsButton_Click(sender, e);
        }

        private void DowntimeEquipmentButton_Click(object? sender, PointerPressedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.DowntimeEquipmentButton_Click(sender, e);
        }

        private void DowntimeSetToday_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.SetDowntimeAnalysisDate(DateTime.Today);
        }

        private void DowntimeSetYesterday_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.SetDowntimeAnalysisDate(DateTime.Today.AddDays(-1));
        }

        /// <summary>Reset-button click hook. The toast is shown by MainWindow via FiltersResetCounter.</summary>
        public void ResetFiltersWithToast_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // No-op: toast is triggered by MainWindow observing FiltersResetCounter.
        }

        private void HeatmapScrollLeft_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var scroller = this.FindControl<ScrollViewer>("DowntimeHeatmapScroller");
            if (scroller != null)
            {
                var offset = scroller.Offset;
                scroller.Offset = new Avalonia.Vector(Math.Max(0, offset.X - 256), offset.Y);
            }
        }

        private void HeatmapScrollRight_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var scroller = this.FindControl<ScrollViewer>("DowntimeHeatmapScroller");
            if (scroller != null)
            {
                var offset = scroller.Offset;
                scroller.Offset = new Avalonia.Vector(offset.X + 256, offset.Y);
            }
        }
    }
}
