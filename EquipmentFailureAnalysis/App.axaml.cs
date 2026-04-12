using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentFailureAnalysis.Services;
using EquipmentFailureAnalysis.ViewModels;
using EquipmentFailureAnalysis.Views;
using EquipmentFailureAnalysis.Utility;
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

                var restored = false;

                // First try to restore last Jira import cache
                try
                {
                    if (PersistedDataStore.TryLoadJiraImportedEquipment(out var jiraItems) && jiraItems.Count > 0)
                    {
                        vm.ImportEquipment(jiraItems);
                        vm.AddStatusEvent($"Восстановлены данные Jira: {jiraItems.Count} ед. оборудования.");
                        restored = true;
                    }
                }
                catch { }

                // Try to restore last imported XML (if exists)
                try
                {
                    if (!restored)
                    {
                        var xmlImportService = new XmlImportService();
                        if (xmlImportService.TryLoadLastImportedEquipment(out var items) && items.Count > 0)
                        {
                            vm.ImportEquipment(items);
                            vm.AddStatusEvent($"Восстановлены данные XML: {items.Count} ед. оборудования.");
                            restored = true;
                        }
                    }
                }
                catch { }

                if (!restored)
                    vm.AddStatusEvent("Сохраненные данные не найдены. Ожидается импорт XML или Jira.");
            }

            base.OnFrameworkInitializationCompleted();
        }

    }
}
