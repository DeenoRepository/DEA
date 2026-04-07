using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentFailureAnalysis.ViewModels;
using EquipmentFailureAnalysis.Views;
using EquipmentFailureAnalysis.Utility;
using System.IO;
using System;

namespace EquipmentFailureAnalysis
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var vm = new MainWindowViewModel();
                desktop.MainWindow = new MainWindow
                {
                    DataContext = vm,
                };

                // Try to restore last imported XML (if exists)
                try
                {
                    var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EquipmentFailureAnalysis");
                    var file = Path.Combine(dir, "last_imported_xml.txt");
                    if (File.Exists(file))
                    {
                        var p = File.ReadAllText(file).Trim();
                        if (!string.IsNullOrEmpty(p) && File.Exists(p))
                        {
                            var decoder = new XmlDataDecoder(p);
                            var items = decoder.DecodeEquipment();
                            vm.ImportEquipment(items);
                        }
                    }
                }
                catch { }
            }

            base.OnFrameworkInitializationCompleted();
        }

    }
}