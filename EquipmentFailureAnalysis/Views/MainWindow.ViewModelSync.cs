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
                prevVm.Dashboard.PropertyChanged -= OnDashboardViewModelPropertyChanged;
            }

            _trackedSettingsVm = DataContext as INotifyPropertyChanged;
            if (_trackedSettingsVm != null)
                _trackedSettingsVm.PropertyChanged += OnViewModelPropertyChanged;
            if (DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
            {
                vm.Reports.PropertyChanged += OnReportsViewModelPropertyChanged;
                vm.Settings.PropertyChanged += OnSettingsViewModelPropertyChanged;
                vm.Dashboard.PropertyChanged += OnDashboardViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressSettingsSave)
                return;

            if (e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.HeatmapColorMin)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.HeatmapColorMax)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.SelectedHeatmapSetting)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.AnalysisDate)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.DowntimeAnalysisDate)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.EmployeeTimelineDate)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.SelectedEquipment)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.SelectedIssueTypeFilter)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.SelectedDowntimeIssueTypeFilter)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.SelectedDowntimeStatusFilter)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.SelectedDowntimeResponsibleFilter)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.SelectedDowntimeSubdivisionFilter)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.DowntimeEquipmentSearchQuery)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.SelectedEmployeeTimelineEmployee))
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

            if (e.PropertyName.StartsWith("Jira", StringComparison.Ordinal)
                || e.PropertyName.StartsWith("Ldap", StringComparison.Ordinal))
                SaveJiraSettingsFromUi();

            if (e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.SettingsViewModel.JiraAutoImportEnabled)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.SettingsViewModel.JiraAutoImportPeriodMinutes))
            {
                ConfigureJiraAutoImportLoop();
            }
        }

        private void OnDashboardViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressSettingsSave)
                return;

            if (e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.DashboardViewModel.SelectedDashboardIssueTypeFilter)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.DashboardViewModel.SelectedDashboardResponsibleFilter)
                || e.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.DashboardViewModel.SelectedDashboardSubdivisionFilter))
            {
                SaveJiraSettingsFromUi();
            }
        }

    }
}
