using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace EquipmentFailureAnalysis.ViewModels
{
    public sealed class SettingsViewModel : ViewModelBase
    {
        private static readonly HashSet<string> ObservedPropertyNames = new(StringComparer.Ordinal)
        {
            nameof(HeatmapSettingOptions),
            nameof(SelectedHeatmapSetting),
            nameof(HeatmapColorMin),
            nameof(HeatmapColorMax),
            nameof(SlaTargetMinutes),
            nameof(StatusMessage)
        };

        private readonly MainWindowViewModel _shell;

        private string _jiraResourceUrl = string.Empty;
        private string _jiraJql = string.Empty;
        private string _jiraUsername = string.Empty;
        private string _jiraToken = string.Empty;
        private string _jiraFilterIdInput = string.Empty;
        private string? _jiraSelectedFilterId;
        private bool _jiraAutoImportEnabled;
        private int _jiraAutoImportPeriodMinutes = 30;

        public SettingsViewModel(MainWindowViewModel shell)
        {
            _shell = shell;
            _shell.PropertyChanged += OnShellPropertyChanged;
        }

        public string JiraResourceUrl
        {
            get => _jiraResourceUrl;
            set => this.RaiseAndSetIfChanged(ref _jiraResourceUrl, value ?? string.Empty);
        }

        public string JiraJql
        {
            get => _jiraJql;
            set => this.RaiseAndSetIfChanged(ref _jiraJql, value ?? string.Empty);
        }

        public string JiraUsername
        {
            get => _jiraUsername;
            set => this.RaiseAndSetIfChanged(ref _jiraUsername, value ?? string.Empty);
        }

        public string JiraToken
        {
            get => _jiraToken;
            set => this.RaiseAndSetIfChanged(ref _jiraToken, value ?? string.Empty);
        }

        public string JiraFilterIdInput
        {
            get => _jiraFilterIdInput;
            set => this.RaiseAndSetIfChanged(ref _jiraFilterIdInput, value ?? string.Empty);
        }

        public string? JiraSelectedFilterId
        {
            get => _jiraSelectedFilterId;
            set => this.RaiseAndSetIfChanged(ref _jiraSelectedFilterId, value);
        }

        public ObservableCollection<string> JiraFilterIds { get; } = new();

        public bool JiraAutoImportEnabled
        {
            get => _jiraAutoImportEnabled;
            set => this.RaiseAndSetIfChanged(ref _jiraAutoImportEnabled, value);
        }

        public int JiraAutoImportPeriodMinutes
        {
            get => _jiraAutoImportPeriodMinutes;
            set
            {
                var normalized = Math.Clamp(value, 1, 1440);
                this.RaiseAndSetIfChanged(ref _jiraAutoImportPeriodMinutes, normalized);
            }
        }

        public ObservableCollection<string> HeatmapSettingOptions => _shell.HeatmapSettingOptions;

        public string SelectedHeatmapSetting
        {
            get => _shell.SelectedHeatmapSetting;
            set => _shell.SelectedHeatmapSetting = value;
        }

        public int HeatmapColorMin
        {
            get => _shell.HeatmapColorMin;
            set => _shell.HeatmapColorMin = value;
        }

        public int HeatmapColorMax
        {
            get => _shell.HeatmapColorMax;
            set => _shell.HeatmapColorMax = value;
        }

        public double SlaTargetMinutes
        {
            get => _shell.SlaTargetMinutes;
            set => _shell.SlaTargetMinutes = value;
        }

        public string StatusMessage => _shell.StatusMessage;

        private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.PropertyName))
                return;

            if (ObservedPropertyNames.Contains(e.PropertyName))
                this.RaisePropertyChanged(e.PropertyName);
        }
    }
}
