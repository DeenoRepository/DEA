using Avalonia.Controls;
using Avalonia.Interactivity;
using EquipmentFailureAnalysis.Utility;
using System.Linq;
using System;

namespace EquipmentFailureAnalysis.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private async void ImportButton_Click(object? sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filters.Add(new FileDialogFilter { Name = "XML files", Extensions = { "xml" } });
            dlg.AllowMultiple = false;
            var res = await dlg.ShowAsync(this);
            if (res == null || res.Length == 0)
                return;
            var path = res.First();

            try
            {
                var decoder = new XmlDataDecoder(path);
                var items = decoder.DecodeEquipment();
                // pass to view model
                if (this.DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                {
                    vm.ImportEquipment(items);
                }
            }
            catch (Exception ex)
            {
                var tb = new TextBlock { Text = "Ошибка при загрузке файла: " + ex.Message };
                var wnd = new Window
                {
                    Title = "Ошибка импорта",
                    Width = 400,
                    Height = 120,
                    Content = tb
                };
                await wnd.ShowDialog(this);
            }
        }
    }
}