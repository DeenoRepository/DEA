using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace EquipmentFailureAnalysis.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ShowAllSubdivisionsToggle_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.DashboardViewModel vm)
            {
                vm.ShowAllSubdivisions = !vm.ShowAllSubdivisions;
            }
        }

        /// <summary>Reset-button click hook. The toast is shown by MainWindow via FiltersResetCounter.</summary>
        public void ResetFiltersWithToast_Click(object? sender, RoutedEventArgs e)
        {
            // No-op: toast is triggered by MainWindow observing FiltersResetCounter.
        }
    }
}
