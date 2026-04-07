using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using EquipmentFailureAnalysis.Utility;
using System.Linq;
using System;
using System.IO;

namespace EquipmentFailureAnalysis.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.GetObservable<Rect>(BoundsProperty).Subscribe(_ => OnWindowResized());
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
                    // persist last imported file path
                    try { SaveLastImportedPath(path); } catch { }
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

        private string GetLastImportedPathFile()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EquipmentFailureAnalysis");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "last_imported_xml.txt");
        }

        private void SaveLastImportedPath(string path)
        {
            var file = GetLastImportedPathFile();
            File.WriteAllText(file, path);
        }

        private string? LoadLastImportedPath()
        {
            var file = GetLastImportedPathFile();
            if (!File.Exists(file)) return null;
            try
            {
                var p = File.ReadAllText(file).Trim();
                return string.IsNullOrEmpty(p) ? null : p;
            }
            catch
            {
                return null;
            }
        }

        private void OnWindowResized()
        {
            // compute ideal DayCellSize so 31 columns fit into central heatmap viewport
            try
            {
                if (this.DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                {
                    // find the heatmap scroller control
                    var scroller = this.FindControl<ScrollViewer>("HeatmapScroller");
                    if (scroller != null)
                    {
                        // leave a larger margin so day cells don't overflow the visible area
                        double available = scroller.Bounds.Width - 120; // padding for labels/margins
                        if (available <= 0) return;
                        // 31 day columns
                        double cellWithMargin = available / 31.0;
                        // subtract extra to avoid overflow when including cell margins
                        double size = Math.Max(12.0, Math.Min(64.0, cellWithMargin - 6.0));
                        vm.DayCellSize = size;
                    }
                }
            }
            catch { }
        }
    }
}