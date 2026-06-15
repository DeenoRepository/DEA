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
            var savedTheme = PersistedDataStore.LoadTheme();
            RequestedThemeVariant = savedTheme switch
            {
                "Light" => Avalonia.Styling.ThemeVariant.Light,
                "Dark" => Avalonia.Styling.ThemeVariant.Dark,
                _ => Avalonia.Styling.ThemeVariant.Default
            };

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
                        vm.AddStatusEvent($"Восстановлены сохраненные данные импорта: {jiraItems.Count} ед. оборудования.");
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

                // Restore last saved active UI state (active page, selected equipment, dates, filters)
                try
                {
                    if (desktop.MainWindow is MainWindow mainWin)
                    {
                        mainWin.RestoreActiveUiState();
                    }
                }
                catch { }
            }

            base.OnFrameworkInitializationCompleted();
        }

    }
}
