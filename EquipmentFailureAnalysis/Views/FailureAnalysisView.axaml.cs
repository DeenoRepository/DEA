using Avalonia.Controls;
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

        /// <summary>Reset-button click hook. The toast is shown by MainWindow via FiltersResetCounter.</summary>
        public void ResetFiltersWithToast_Click(object? sender, RoutedEventArgs e)
        {
            // No-op: toast is triggered by MainWindow observing FiltersResetCounter.
        }
    }
}
