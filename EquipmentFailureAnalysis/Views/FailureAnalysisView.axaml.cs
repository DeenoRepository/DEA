using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace EquipmentFailureAnalysis.Views
{
    public partial class FailureAnalysisView : UserControl
    {
        public FailureAnalysisView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void FailureHeatmapSettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.FailureHeatmapSettingsButton_Click(sender, e);
        }

        private async void CopyInventoryNumberButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainWindowViewModel vm && vm.SelectedEquipment != null && !string.IsNullOrWhiteSpace(vm.SelectedEquipment.InventoryNumber))
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(vm.SelectedEquipment.InventoryNumber);
                    vm.StatusMessage = $"Инвентарный номер скопирован: {vm.SelectedEquipment.InventoryNumber}";
                }
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

        private void FailureEventRow_PointerPressed(object? sender, PointerPressedEventArgs e)
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

        /// <summary>Reset-button click hook. The toast is shown by MainWindow via FiltersResetCounter.</summary>
        public void ResetFiltersWithToast_Click(object? sender, RoutedEventArgs e)
        {
            // No-op: toast is triggered by MainWindow observing FiltersResetCounter.
        }

        private void HeatmapScrollLeft_Click(object? sender, RoutedEventArgs e)
        {
            var scroller = this.FindControl<ScrollViewer>("FailureHeatmapScroller");
            if (scroller != null)
            {
                var offset = scroller.Offset;
                scroller.Offset = new Avalonia.Vector(Math.Max(0, offset.X - 256), offset.Y);
            }
        }

        private void HeatmapScrollRight_Click(object? sender, RoutedEventArgs e)
        {
            var scroller = this.FindControl<ScrollViewer>("FailureHeatmapScroller");
            if (scroller != null)
            {
                var offset = scroller.Offset;
                scroller.Offset = new Avalonia.Vector(offset.X + 256, offset.Y);
            }
        }
    }
}
