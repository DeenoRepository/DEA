using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace EquipmentFailureAnalysis.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void JiraFilterIdAddButton_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.JiraFilterIdAddButton_Click(sender, e);
        }

        private void JiraFilterIdRemoveButton_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.JiraFilterIdRemoveButton_Click(sender, e);
        }

        private void JiraSettingsField_TextChanged(object? sender, Avalonia.Controls.TextChangedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.JiraSettingsField_TextChanged(sender, e);
        }

        private void ImportButton_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.ImportButton_Click(sender, e);
        }

        private void ImportFromJiraButton_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.ImportFromJiraButton_Click(sender, e);
        }

        private void TestJiraConnectionButton_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.TestJiraConnectionButton_Click(sender, e);
        }

        private void TestLdapConnectionButton_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.TestLdapConnectionButton_Click(sender, e);
        }
    }
}
