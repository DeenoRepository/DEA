using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using EquipmentFailureAnalysis.Utility;
using System.Linq;
using System;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;

namespace EquipmentFailureAnalysis.Views
{
    public partial class MainWindow
    {
        private void HookSettingsPersistence()
        {
            if (_trackedSettingsVm != null)
                _trackedSettingsVm.PropertyChanged -= OnViewModelPropertyChanged;
            if (_trackedSettingsVm is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel prevVm)
            {
                prevVm.Reports.PropertyChanged -= OnReportsViewModelPropertyChanged;
                prevVm.Settings.PropertyChanged -= OnSettingsViewModelPropertyChanged;
            }

            _trackedSettingsVm = DataContext as INotifyPropertyChanged;
            if (_trackedSettingsVm != null)
                _trackedSettingsVm.PropertyChanged += OnViewModelPropertyChanged;
            if (DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
            {
                vm.Reports.PropertyChanged += OnReportsViewModelPropertyChanged;
                vm.Settings.PropertyChanged += OnSettingsViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressSettingsSave)
                return;

            if (e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.HeatmapColorMin)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.HeatmapColorMax)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.SelectedHeatmapSetting))
            {
                SaveJiraSettingsFromUi();
            }
        }

        private void OnReportsViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressSettingsSave)
                return;

            if (string.IsNullOrWhiteSpace(e.PropertyName))
                return;

            if (e.PropertyName.StartsWith("Report", StringComparison.Ordinal))
                SaveJiraSettingsFromUi();
        }

        private void OnSettingsViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressSettingsSave)
                return;

            if (string.IsNullOrWhiteSpace(e.PropertyName))
                return;

            if (e.PropertyName.StartsWith("Jira", StringComparison.Ordinal))
                SaveJiraSettingsFromUi();
        }

    }
}
