using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

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
    }
}
