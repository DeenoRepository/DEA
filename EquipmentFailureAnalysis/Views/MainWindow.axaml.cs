using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Data;
using EquipmentFailureAnalysis.Services;
using EquipmentFailureAnalysis.Utility;
using EquipmentFailureAnalysis.Models;
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
using System.Threading;
using System.Threading.Tasks;

namespace EquipmentFailureAnalysis.Views
{
    public partial class MainWindow : Window
    {
        private readonly JiraSettingsStore _jiraSettingsStore = new JiraSettingsStore();
        private readonly HtmlReportService _htmlReportService = new HtmlReportService();
        private readonly JiraImportService _jiraImportService = new JiraImportService();
        private readonly XmlImportService _xmlImportService = new XmlImportService();
        private readonly LdapAuthenticationService _ldapAuthenticationService = new LdapAuthenticationService();
        private readonly DispatcherTimer _settingsSaveDebounceTimer;
        private readonly SemaphoreSlim _jiraImportLock = new SemaphoreSlim(1, 1);
        private bool _suppressSettingsSave;
        private bool _startupLdapAuthHandled;
        private INotifyPropertyChanged? _trackedSettingsVm;
        private CancellationTokenSource? _jiraAutoImportCts;
        private Task? _jiraAutoImportTask;
        private DateTime? _lastSuccessfulJiraImportUtc;

        private DateTime _lastEquipmentMenuOpenUtc = DateTime.MinValue;
        private AppPage _currentPage = AppPage.Dashboard;

        private DashboardView? _dashboardPage;
        private FailureAnalysisView? _failureAnalysisPage;
        private DowntimeView? _downtimeAnalysisPage;
        private ReportsView? _reportsPage;
        private SettingsView? _settingsPage;
        private EmployeeAnalysisView? _employeeAnalysisPage;

        private enum AppPage
        {
            Dashboard,
            FailureAnalysis,
            DowntimeAnalysis,
            Reports,
            Settings,
            EmployeeAnalysis
        }

        internal async void ImportFromJiraButton_Click(object? sender, RoutedEventArgs e)
        {
            await RunJiraImportAsync(showDialogs: true, sourceLabel: "Импорт Jira", isAutoImport: false, autoInterval: null);
        }

        private void ApplyParsedResults(ObservableCollection<EquipmentFailureAnalysis.Models.EquipmentInfo> items)
        {
            if (this.DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            vm.ImportEquipment(items);
        }

        private (int added, int updated) MergeParsedResults(ObservableCollection<EquipmentFailureAnalysis.Models.EquipmentInfo> incrementalItems)
        {
            if (this.DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return (0, 0);

            var merged = CloneEquipmentCollection(vm.GetEquipmentForReports());
            var equipmentMap = new Dictionary<string, EquipmentFailureAnalysis.Models.EquipmentInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var equipment in merged)
            {
                var identity = GetEquipmentIdentity(equipment);
                if (!equipmentMap.ContainsKey(identity))
                    equipmentMap[identity] = equipment;
            }
            var added = 0;
            var updated = 0;

            foreach (var incomingEquipment in incrementalItems ?? new ObservableCollection<EquipmentFailureAnalysis.Models.EquipmentInfo>())
            {
                var incomingIdentity = GetEquipmentIdentity(incomingEquipment);
                if (!equipmentMap.TryGetValue(incomingIdentity, out var targetEquipment))
                {
                    targetEquipment = CloneEquipment(incomingEquipment);
                    merged.Add(targetEquipment);
                    equipmentMap[incomingIdentity] = targetEquipment;
                    added += targetEquipment.Issues.Count;
                    continue;
                }

                var issueMap = new Dictionary<string, EquipmentFailureAnalysis.Models.Issue>(StringComparer.OrdinalIgnoreCase);
                foreach (var existing in targetEquipment.Issues)
                {
                    var existingIdentity = GetIssueIdentity(existing);
                    if (!issueMap.ContainsKey(existingIdentity))
                        issueMap[existingIdentity] = existing;
                }
                foreach (var incomingIssue in incomingEquipment.Issues)
                {
                    var issueIdentity = GetIssueIdentity(incomingIssue);
                    if (!issueMap.TryGetValue(issueIdentity, out var existingIssue))
                    {
                        var copy = CloneIssue(incomingIssue);
                        targetEquipment.Issues.Add(copy);
                        issueMap[issueIdentity] = copy;
                        added++;
                        continue;
                    }

                    if (ApplyIssueUpdates(existingIssue, incomingIssue))
                        updated++;
                }
            }

            vm.ImportEquipment(merged);
            PersistedDataStore.SaveJiraImportedEquipment(merged);
            return (added, updated);
        }

        private static ObservableCollection<EquipmentFailureAnalysis.Models.EquipmentInfo> CloneEquipmentCollection(IEnumerable<EquipmentFailureAnalysis.Models.EquipmentInfo> source)
        {
            return new ObservableCollection<EquipmentFailureAnalysis.Models.EquipmentInfo>(
                (source ?? Enumerable.Empty<EquipmentFailureAnalysis.Models.EquipmentInfo>())
                    .Select(CloneEquipment));
        }

        private static EquipmentFailureAnalysis.Models.EquipmentInfo CloneEquipment(EquipmentFailureAnalysis.Models.EquipmentInfo source)
        {
            var copy = new EquipmentFailureAnalysis.Models.EquipmentInfo
            {
                Uid = source.Uid,
                Title = source.Title,
                InventoryNumber = source.InventoryNumber,
                Subdivision = source.Subdivision
            };

            foreach (var issue in source.Issues)
                copy.Issues.Add(CloneIssue(issue));

            return copy;
        }

        private static EquipmentFailureAnalysis.Models.Issue CloneIssue(EquipmentFailureAnalysis.Models.Issue source)
        {
            return new EquipmentFailureAnalysis.Models.Issue
            {
                Start = source.Start,
                End = source.End,
                Description = source.Description,
                Type = source.Type,
                Responsible = source.Responsible,
                DetectedType = source.DetectedType,
                TypeSuspicious = source.TypeSuspicious,
                RepairProbability = source.RepairProbability,
                SetupProbability = source.SetupProbability,
                IsInProgress = source.IsInProgress,
                JiraIssueKey = source.JiraIssueKey,
                Reporter = source.Reporter,
                Comments = source.Comments
            };
        }

        private static string GetEquipmentIdentity(EquipmentFailureAnalysis.Models.EquipmentInfo equipment)
        {
            if (equipment.Uid != 0)
                return $"uid:{equipment.Uid}|sub:{(equipment.Subdivision ?? string.Empty).Trim()}";

            var inv = (equipment.InventoryNumber ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(inv))
                return $"inv:{inv}|sub:{(equipment.Subdivision ?? string.Empty).Trim()}";

            return $"title:{(equipment.Title ?? string.Empty).Trim()}|sub:{(equipment.Subdivision ?? string.Empty).Trim()}";
        }

        private static string GetIssueIdentity(EquipmentFailureAnalysis.Models.Issue issue)
        {
            if (!string.IsNullOrWhiteSpace(issue.JiraIssueKey))
                return $"jira:{issue.JiraIssueKey.Trim()}";

            return $"sig:{issue.Start:O}|{issue.End:O}|{issue.Type}|{(issue.Responsible ?? string.Empty).Trim()}|{(issue.Description ?? string.Empty).Trim()}";
        }

        private static bool ApplyIssueUpdates(EquipmentFailureAnalysis.Models.Issue target, EquipmentFailureAnalysis.Models.Issue source)
        {
            var changed = false;

            if (target.Start != source.Start) { target.Start = source.Start; changed = true; }
            if (target.End != source.End) { target.End = source.End; changed = true; }
            if (!string.Equals(target.Description, source.Description, StringComparison.Ordinal)) { target.Description = source.Description; changed = true; }
            if (target.Type != source.Type) { target.Type = source.Type; changed = true; }
            if (!string.Equals(target.Responsible, source.Responsible, StringComparison.Ordinal)) { target.Responsible = source.Responsible; changed = true; }
            if (target.DetectedType != source.DetectedType) { target.DetectedType = source.DetectedType; changed = true; }
            if (target.TypeSuspicious != source.TypeSuspicious) { target.TypeSuspicious = source.TypeSuspicious; changed = true; }
            if (Math.Abs(target.RepairProbability - source.RepairProbability) > 0.0001) { target.RepairProbability = source.RepairProbability; changed = true; }
            if (Math.Abs(target.SetupProbability - source.SetupProbability) > 0.0001) { target.SetupProbability = source.SetupProbability; changed = true; }
            if (target.IsInProgress != source.IsInProgress) { target.IsInProgress = source.IsInProgress; changed = true; }
            if (!string.Equals(target.JiraIssueKey, source.JiraIssueKey, StringComparison.OrdinalIgnoreCase)) { target.JiraIssueKey = source.JiraIssueKey; changed = true; }
            if (!string.Equals(target.Reporter, source.Reporter, StringComparison.Ordinal)) { target.Reporter = source.Reporter; changed = true; }
            if (!string.Equals(target.Comments, source.Comments, StringComparison.Ordinal)) { target.Comments = source.Comments; changed = true; }

            return changed;
        }

        private (string Url, string Username, string Token, string Jql, IReadOnlyCollection<string> FilterIds) GetJiraApiSettingsFromUi()
        {
            var settings = (DataContext as EquipmentFailureAnalysis.ViewModels.MainWindowViewModel)?.Settings;
            var url = settings?.JiraResourceUrl?.Trim() ?? string.Empty;
            var username = settings?.JiraUsername?.Trim() ?? string.Empty;
            var token = settings?.JiraToken ?? string.Empty;
            var jql = settings?.JiraJql?.Trim() ?? string.Empty;
            return (url, username, token, jql, GetFilterIdsFromUi());
        }

        private IReadOnlyCollection<string> GetFilterIdsFromUi()
        {
            var list = ((DataContext as EquipmentFailureAnalysis.ViewModels.MainWindowViewModel)?.Settings?.JiraFilterIds ?? new ObservableCollection<string>())
                .Select(v => v?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .Where(v => v.All(char.IsDigit))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return list;
        }

        private async System.Threading.Tasks.Task ShowMessageAsync(string title, string message)
        {
            var wnd = new Window
            {
                Title = title,
                Width = 480,
                Height = 160,
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    Margin = new Thickness(14)
                }
            };

            await wnd.ShowDialog(this);
        }

        private void PublishStatus(string message)
        {
            void Apply()
            {
                if (DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                {
                    vm.AddStatusEvent(message);

                    // Determine Toast type
                    string toastType = "Info";
                    if (message.Contains("Ошибка", StringComparison.OrdinalIgnoreCase) || 
                        message.Contains("ошибка", StringComparison.OrdinalIgnoreCase))
                    {
                        toastType = "Error";
                    }
                    else if (message.Contains("успешно", StringComparison.OrdinalIgnoreCase) || 
                             message.Contains("Успешно", StringComparison.OrdinalIgnoreCase) || 
                             message.Contains("завершен", StringComparison.OrdinalIgnoreCase) || 
                             message.Contains("завершено", StringComparison.OrdinalIgnoreCase))
                    {
                        toastType = "Success";
                    }

                    // Don't show toast for initial boot status
                    if (!message.Equals("Приложение готово к работе.", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowToast(message, toastType);
                    }
                }
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                Apply();
                return;
            }

            Dispatcher.UIThread.Post(Apply);
        }

        private IBrush GetThemeBrush(string key, string fallbackHex)
        {
            try
            {
                if (this.FindResource(key) is IBrush brush)
                {
                    return brush;
                }
            }
            catch { }

            try
            {
                if (Application.Current != null && Application.Current.FindResource(key) is IBrush appBrush)
                {
                    return appBrush;
                }
            }
            catch { }

            try
            {
                return Brush.Parse(fallbackHex);
            }
            catch
            {
                return Brushes.Gray;
            }
        }

        public void ShowToast(string message, string type = "Info")
        {
            var container = FindNestedControl<StackPanel>("ToastContainer");
            if (container == null)
                return;

            var toast = new Border
            {
                Classes = { "toastCard", type.ToLowerInvariant() }
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 12
            };

            var iconGlyph = type.ToLowerInvariant() switch
            {
                "success" => "\uE8FB", // Accept/Checkmark
                "error" => "\uEA39",   // Error round
                _ => "\uE946"          // Info round
            };

            var iconBrush = type.ToLowerInvariant() switch
            {
                "success" => GetThemeBrush("SuccessBrush", "#059669"),
                "error" => GetThemeBrush("DangerBrush", "#DC2626"),
                _ => GetThemeBrush("InfoBrush", "#0EA5E9")
            };

            var icon = new TextBlock
            {
                Text = iconGlyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 18,
                Foreground = iconBrush,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            var textStack = new StackPanel
            {
                Spacing = 2,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            var titleText = type.ToLowerInvariant() switch
            {
                "success" => "Успешно",
                "error" => "Ошибка",
                _ => "Уведомление"
            };

            var title = new TextBlock
            {
                Text = titleText,
                FontWeight = FontWeight.SemiBold,
                FontSize = 12,
                Foreground = GetThemeBrush("TextPrimary", "#0F172A")
            };
            textStack.Children.Add(title);

            var content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = GetThemeBrush("TextSecondary", "#475569")
            };
            textStack.Children.Add(content);
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            var closeButton = new Button
            {
                Content = new TextBlock
                {
                    Text = "\uE711",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 10,
                    Foreground = GetThemeBrush("TextMuted", "#94A3B8")
                },
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            Grid.SetColumn(closeButton, 2);
            grid.Children.Add(closeButton);

            toast.Child = grid;

            // Close action
            async void CloseToast()
            {
                toast.Classes.Remove("visible");
                await Task.Delay(300);
                container.Children.Remove(toast);
            }

            closeButton.Click += (_, _) => CloseToast();

            container.Children.Add(toast);

            // Trigger animation in next frame
            Dispatcher.UIThread.Post(() => toast.Classes.Add("visible"));

            // Auto dismiss after 5 seconds (errors stay until manually closed)
            if (!string.Equals(type, "error", StringComparison.OrdinalIgnoreCase))
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    CloseToast();
                };
                timer.Start();
            }
        }

        private void EnsurePagesInitialized()
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            if (_dashboardPage == null)
                _dashboardPage = new DashboardView();
            _dashboardPage.DataContext = vm.Dashboard;

            if (_failureAnalysisPage == null)
                _failureAnalysisPage = new FailureAnalysisView();
            _failureAnalysisPage.DataContext = vm;

            if (_downtimeAnalysisPage == null)
                _downtimeAnalysisPage = new DowntimeView();
            _downtimeAnalysisPage.DataContext = vm.Downtime;

            if (_reportsPage == null)
                _reportsPage = new ReportsView();
            _reportsPage.DataContext = vm.Reports;

            if (_settingsPage == null)
                _settingsPage = new SettingsView();
            _settingsPage.DataContext = vm.Settings;

            if (_employeeAnalysisPage == null)
                _employeeAnalysisPage = new EmployeeAnalysisView();
            _employeeAnalysisPage.DataContext = vm;
        }

        private T? FindNestedControl<T>(string name) where T : Control
        {
            var direct = this.FindControl<T>(name);
            if (direct != null)
                return direct;

            var visual = this.GetVisualDescendants()
                .OfType<T>()
                .FirstOrDefault(control =>
                    control is StyledElement styled &&
                    string.Equals(styled.Name, name, StringComparison.Ordinal));
            if (visual != null)
                return visual;

            return this.GetLogicalDescendants()
                .OfType<T>()
                .FirstOrDefault(control =>
                    control is StyledElement styled &&
                    string.Equals(styled.Name, name, StringComparison.Ordinal));
        }

        private async Task EnsureLdapAuthorizationOnStartupAsync()
        {
            if (_startupLdapAuthHandled)
                return;

            _startupLdapAuthHandled = true;

            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            var settings = vm.Settings;
            if (!settings.LdapAuthEnabled)
                return;

            var loginRequest = new LdapLoginRequest
            {
                Server = settings.LdapServer.Trim(),
                Port = settings.LdapPort > 0 ? settings.LdapPort : (settings.LdapUseSsl ? 636 : 389),
                UseSsl = settings.LdapUseSsl,
                Domain = settings.LdapDomain?.Trim() ?? string.Empty,
                BaseDn = settings.LdapBaseDn?.Trim() ?? string.Empty,
                InitialUsername = settings.LdapLastUsername?.Trim() ?? string.Empty
            };

            var dialog = new LdapLoginWindow(loginRequest, _ldapAuthenticationService);

            var authResult = await dialog.ShowDialog<LdapLoginResult?>(this);

            settings.LdapServer = loginRequest.Server?.Trim() ?? string.Empty;
            settings.LdapPort = loginRequest.Port > 0 ? loginRequest.Port : 389;
            settings.LdapUseSsl = loginRequest.UseSsl;
            settings.LdapDomain = loginRequest.Domain?.Trim() ?? string.Empty;
            settings.LdapBaseDn = loginRequest.BaseDn?.Trim() ?? string.Empty;

            if (authResult?.Success != true)
            {
                SaveJiraSettingsFromUi(immediate: true);
                Close();
                return;
            }

            settings.LdapLastUsername = authResult.Username?.Trim() ?? string.Empty;
            SaveJiraSettingsFromUi(immediate: true);
            PublishStatus($"LDAP авторизация успешна: {settings.LdapLastUsername}");
        }

        public MainWindow()
        {
            _settingsSaveDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _settingsSaveDebounceTimer.Tick += (_, _) =>
            {
                _settingsSaveDebounceTimer.Stop();
                SaveJiraSettingsFromUi(immediate: true);
            };

            InitializeComponent();
            Opened += async (_, _) => await EnsureLdapAuthorizationOnStartupAsync();
            ApplyNavigationPanelState();
            InitializeReportTools();
            LoadJiraSettingsToUi();
            this.GetObservable<Rect>(BoundsProperty).Subscribe(_ => OnWindowResized());
            UpdatePageVisibility();

            // Observe filter reset counter to show confirmation toast
            this.DataContextChanged += (_, _) =>
            {
                if (DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                {
                    vm.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.FiltersResetCounter))
                        {
                            ShowToast("Фильтры сброшены", "Success");
                        }
                        else if (args.PropertyName == nameof(EquipmentFailureAnalysis.ViewModels.MainWindowViewModel.IsLoading))
                        {
                            UpdateLiveStatusPill();
                        }
                    };
                }
            };

            // Hot-keys (Ctrl+1..5 for navigation, Ctrl+B for sidebar, Ctrl+R for right panel,
            // Ctrl+Shift+T for theme). F5 is wired through Window.KeyBindings in XAML.
            KeyDown += MainWindow_KeyDown;

            Dispatcher.UIThread.Post(() =>
            {
                ApplyNavigationPanelState();
                ApplyRightPanelState();
                UpdateThemeGlyph();
            }, DispatcherPriority.Loaded);
            PublishStatus("Приложение готово к работе.");
        }

        private void MainWindow_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            var mods = e.KeyModifiers;
            if ((mods & Avalonia.Input.KeyModifiers.Control) == 0)
                return;

            switch (e.Key)
            {
                case Avalonia.Input.Key.D1:
                    DashboardButton_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.D2:
                    DowntimeAnalysisButton_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.D3:
                    EmployeeAnalysisButton_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.D4:
                    ReportsButton_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.D5:
                    SettingsButton_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.B:
                    ToggleNavigationButton_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.R:
                    ToggleRightPanelButton_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
                    e.Handled = true;
                    break;
                case Avalonia.Input.Key.T when (mods & Avalonia.Input.KeyModifiers.Shift) != 0:
                    ToggleThemeButton_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
        }

        private void UpdateThemeGlyph()
        {
            if (this.FindControl<Avalonia.Controls.TextBlock>("ThemeToggleGlyph") is { } glyph &&
                Avalonia.Application.Current is { } app)
            {
                glyph.Text = app.RequestedThemeVariant == Avalonia.Styling.ThemeVariant.Dark
                    ? "\uE7E8"  // Sun — currently dark, click to go light
                    : "\uE708"; // Moon — currently light, click to go dark
            }
        }

        private void UpdateLiveStatusPill()
        {
            if (this.FindControl<Avalonia.Controls.TextBlock>("LiveStatusText") is not { } txt ||
                this.FindControl<Avalonia.Controls.Border>("LiveStatusPill") is not { } pill ||
                this.FindControl<Avalonia.Controls.TextBlock>("LiveStatusIcon") is not { } icon)
                return;

            bool isLoading = DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm && vm.IsLoading;
            if (isLoading)
            {
                txt.Text = "Импорт…";
                pill.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#2A1F0A"));
                pill.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#5A4515"));
                icon.Text = "\uE895"; // Sync
            }
            else
            {
                txt.Text = "Online";
                pill.Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#11203D"));
                pill.BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#1F2A4A"));
                icon.Text = "\uE73E"; // CheckMark
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            StopJiraAutoImportLoop();
            SaveJiraSettingsFromUi(immediate: true);
            base.OnClosed(e);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            HookSettingsPersistence();
            LoadJiraSettingsToUi();
            ConfigureJiraAutoImportLoop();
            UpdatePageVisibility();
            HookUserProfileUpdates();
        }

        internal void JiraFilterIdAddButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            var settings = vm.Settings;
            var filterId = settings.JiraFilterIdInput?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(filterId) || !filterId.All(char.IsDigit))
                return;

            if (settings.JiraFilterIds.Any(v => string.Equals(v, filterId, StringComparison.Ordinal)))
                return;

            settings.JiraFilterIds.Add(filterId);
            settings.JiraFilterIdInput = string.Empty;
            SaveJiraSettingsFromUi();
        }

        internal void JiraFilterIdRemoveButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                return;

            var selected = vm.Settings.JiraSelectedFilterId;
            if (string.IsNullOrWhiteSpace(selected))
                return;

            vm.Settings.JiraFilterIds.Remove(selected);
            vm.Settings.JiraSelectedFilterId = null;
            SaveJiraSettingsFromUi();
        }

        internal void JiraSettingsField_TextChanged(object? sender, TextChangedEventArgs e)
        {
            SaveJiraSettingsFromUi();
        }

        internal void ReportSettingsChanged()
        {
            SaveJiraSettingsFromUi();
        }


        private static string NormalizeReportGroupByKey(string? value)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
                return "day";

            if (string.Equals(normalized, "day", StringComparison.OrdinalIgnoreCase))
                return "day";
            if (string.Equals(normalized, "month", StringComparison.OrdinalIgnoreCase))
                return "month";
            if (string.Equals(normalized, "employee", StringComparison.OrdinalIgnoreCase))
                return "employee";
            if (string.Equals(normalized, "equipment", StringComparison.OrdinalIgnoreCase))
                return "equipment";

            if (normalized.Contains("меся", StringComparison.CurrentCultureIgnoreCase))
                return "month";
            if (normalized.Contains("сотруд", StringComparison.CurrentCultureIgnoreCase))
                return "employee";
            if (normalized.Contains("оборуд", StringComparison.CurrentCultureIgnoreCase))
                return "equipment";

            return "day";
        }

        private void SaveJiraSettingsFromUi(bool immediate = false)
        {
            if (_suppressSettingsSave)
                return;

            if (!immediate)
            {
                _settingsSaveDebounceTimer.Stop();
                _settingsSaveDebounceTimer.Start();
                return;
            }

            try
            {
                var existing = _jiraSettingsStore.TryLoad("jira_import_settings.json", out JiraImportSettings? loaded) && loaded != null
                    ? loaded
                    : new JiraImportSettings();

                var shellVm = DataContext as EquipmentFailureAnalysis.ViewModels.MainWindowViewModel;
                var settingsVm = shellVm?.Settings;
                var reportsVm = shellVm?.Reports;

                var settings = new JiraImportSettings
                {
                    JiraResourceUrl = settingsVm?.JiraResourceUrl?.Trim() ?? existing.JiraResourceUrl,
                    JiraUsername = settingsVm?.JiraUsername?.Trim() ?? existing.JiraUsername,
                    JiraJql = settingsVm?.JiraJql?.Trim() ?? existing.JiraJql,
                    JiraAutoImportEnabled = settingsVm?.JiraAutoImportEnabled ?? existing.JiraAutoImportEnabled,
                    JiraAutoImportPeriodMinutes = settingsVm != null ? Math.Clamp(settingsVm.JiraAutoImportPeriodMinutes, 1, 1440) : Math.Clamp(existing.JiraAutoImportPeriodMinutes <= 0 ? 30 : existing.JiraAutoImportPeriodMinutes, 1, 1440),
                    JiraLastSuccessfulImportUtc = _lastSuccessfulJiraImportUtc ?? existing.JiraLastSuccessfulImportUtc,
                    JiraFilterIds = settingsVm != null ? GetFilterIdsFromUi().ToList() : (existing.JiraFilterIds ?? new List<string>()),
                    HeatmapSelectedSetting = shellVm?.SelectedHeatmapSetting ?? string.Empty,
                    FailureHeatmapMin = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.FailureHeatmapKey).Min,
                    FailureHeatmapMax = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.FailureHeatmapKey).Max,
                    DowntimeHeatmapMin = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.DowntimeHeatmapKey).Min,
                    DowntimeHeatmapMax = ValueToColorConverter.GetHeatmapRange(ValueToColorConverter.DowntimeHeatmapKey).Max,
                    ReportStartDate = reportsVm?.ReportStartDate?.Date ?? existing.ReportStartDate,
                    ReportEndDate = reportsVm?.ReportEndDate?.Date ?? existing.ReportEndDate,
                    ReportGroupBy = reportsVm != null ? NormalizeReportGroupByKey(reportsVm.ReportGroupByKey) : NormalizeReportGroupByKey(existing.ReportGroupBy),
                    ReportIncludeDashboard = reportsVm?.ReportIncludeDashboard ?? existing.ReportIncludeDashboard,
                    ReportIncludeDowntime = reportsVm?.ReportIncludeDowntime ?? existing.ReportIncludeDowntime,
                    ReportIncludeEmployee = reportsVm?.ReportIncludeEmployee ?? existing.ReportIncludeEmployee,
                    ReportOpenAfterGenerate = reportsVm?.ReportOpenAfterGenerate ?? existing.ReportOpenAfterGenerate,
                    ReportOnlyInProgress = reportsVm?.ReportOnlyInProgress ?? existing.ReportOnlyInProgress,
                    ReportFilterByDuration = reportsVm?.ReportFilterByDuration ?? existing.ReportFilterByDuration,
                    ReportMinDurationMinutes = reportsVm != null ? (int)Math.Round(reportsVm.ReportMinDurationMinutes) : existing.ReportMinDurationMinutes,
                    ReportFieldStart = reportsVm?.ReportFieldStart ?? existing.ReportFieldStart,
                    ReportFieldEnd = reportsVm?.ReportFieldEnd ?? existing.ReportFieldEnd,
                    ReportFieldEquipment = reportsVm?.ReportFieldEquipment ?? existing.ReportFieldEquipment,
                    ReportFieldSubdivision = reportsVm?.ReportFieldSubdivision ?? existing.ReportFieldSubdivision,
                    ReportFieldType = reportsVm?.ReportFieldType ?? existing.ReportFieldType,
                    ReportFieldResponsible = reportsVm?.ReportFieldResponsible ?? existing.ReportFieldResponsible,
                    ReportFieldDescription = reportsVm?.ReportFieldDescription ?? existing.ReportFieldDescription,
                    ReportLastFilePath = reportsVm?.ReportLastFilePath ?? existing.ReportLastFilePath,
                    LdapAuthEnabled = settingsVm?.LdapAuthEnabled ?? existing.LdapAuthEnabled,
                    LdapServer = settingsVm?.LdapServer?.Trim() ?? existing.LdapServer,
                    LdapPort = settingsVm != null
                        ? Math.Clamp(settingsVm.LdapPort <= 0 ? 389 : settingsVm.LdapPort, 1, 65535)
                        : Math.Clamp(existing.LdapPort <= 0 ? 389 : existing.LdapPort, 1, 65535),
                    LdapUseSsl = settingsVm?.LdapUseSsl ?? existing.LdapUseSsl,
                    LdapDomain = settingsVm?.LdapDomain?.Trim() ?? existing.LdapDomain,
                    LdapBaseDn = settingsVm?.LdapBaseDn?.Trim() ?? existing.LdapBaseDn,
                    LdapLastUsername = settingsVm?.LdapLastUsername?.Trim() ?? existing.LdapLastUsername,

                    LastActivePage = _currentPage.ToString(),
                    AnalysisDate = shellVm?.AnalysisDate,
                    DowntimeAnalysisDate = shellVm?.DowntimeAnalysisDate,
                    EmployeeTimelineDate = shellVm?.EmployeeTimelineDate,
                    SelectedEquipmentUid = shellVm?.SelectedEquipment?.Uid,
                    SelectedIssueTypeFilter = shellVm?.SelectedIssueTypeFilter ?? "Все позиции",
                    SelectedDowntimeIssueTypeFilter = shellVm?.SelectedDowntimeIssueTypeFilter ?? "Все типы",
                    SelectedDowntimeStatusFilter = shellVm?.SelectedDowntimeStatusFilter ?? "Все статусы",
                    SelectedDowntimeResponsibleFilter = shellVm?.SelectedDowntimeResponsibleFilter ?? "Все ответственные",
                    SelectedDowntimeSubdivisionFilter = shellVm?.SelectedDowntimeSubdivisionFilter ?? "Все группы",
                    DowntimeEquipmentSearchQuery = shellVm?.DowntimeEquipmentSearchQuery ?? string.Empty,
                    SelectedDashboardIssueTypeFilter = shellVm?.Dashboard?.SelectedDashboardIssueTypeFilter ?? "Все типы",
                    SelectedDashboardResponsibleFilter = shellVm?.Dashboard?.SelectedDashboardResponsibleFilter ?? "Все ответственные",
                    SelectedDashboardSubdivisionFilter = shellVm?.Dashboard?.SelectedDashboardSubdivisionFilter ?? "Все группы",
                    SelectedEmployeeTimelineEmployee = shellVm?.SelectedEmployeeTimelineEmployee ?? "Все сотрудники"
                };

                _jiraSettingsStore.Save("jira_import_settings.json", settings);
            }
            catch
            {
                // ignore persistence errors
            }
        }

        private void LoadJiraSettingsToUi()
        {
            try
            {
                _suppressSettingsSave = true;

                if (!_jiraSettingsStore.TryLoad("jira_import_settings.json", out JiraImportSettings? settings) || settings == null)
                    return;

                _lastSuccessfulJiraImportUtc = settings.JiraLastSuccessfulImportUtc;

                if (DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel shellSettingsVm)
                {
                    shellSettingsVm.Settings.JiraResourceUrl = settings.JiraResourceUrl ?? string.Empty;
                    shellSettingsVm.Settings.JiraUsername = settings.JiraUsername ?? string.Empty;
                    shellSettingsVm.Settings.JiraJql = settings.JiraJql ?? string.Empty;
                    shellSettingsVm.Settings.JiraAutoImportEnabled = settings.JiraAutoImportEnabled;
                    shellSettingsVm.Settings.JiraAutoImportPeriodMinutes = Math.Clamp(settings.JiraAutoImportPeriodMinutes <= 0 ? 30 : settings.JiraAutoImportPeriodMinutes, 1, 1440);
                    shellSettingsVm.Settings.JiraFilterIds.Clear();
                    var loadedIds = settings.JiraFilterIds ?? new List<string>();
                    foreach (var filterId in loadedIds.Where(v => !string.IsNullOrWhiteSpace(v) && v.All(char.IsDigit)).Distinct(StringComparer.Ordinal))
                        shellSettingsVm.Settings.JiraFilterIds.Add(filterId.Trim());
                    shellSettingsVm.Settings.JiraSelectedFilterId = null;
                    shellSettingsVm.Settings.LdapAuthEnabled = settings.LdapAuthEnabled;
                    shellSettingsVm.Settings.LdapServer = settings.LdapServer ?? string.Empty;
                    shellSettingsVm.Settings.LdapPort = Math.Clamp(settings.LdapPort <= 0 ? 389 : settings.LdapPort, 1, 65535);
                    shellSettingsVm.Settings.LdapUseSsl = settings.LdapUseSsl;
                    shellSettingsVm.Settings.LdapDomain = settings.LdapDomain ?? string.Empty;
                    shellSettingsVm.Settings.LdapBaseDn = settings.LdapBaseDn ?? string.Empty;
                    shellSettingsVm.Settings.LdapLastUsername = settings.LdapLastUsername ?? string.Empty;
                }

                if (DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                {
                    vm.SelectFailureHeatmapSettings();
                    vm.HeatmapColorMin = Math.Max(0, settings.FailureHeatmapMin);
                    vm.HeatmapColorMax = Math.Max(settings.FailureHeatmapMax, settings.FailureHeatmapMin + 1);

                    vm.SelectDowntimeHeatmapSettings();
                    vm.HeatmapColorMin = Math.Max(0, settings.DowntimeHeatmapMin);
                    vm.HeatmapColorMax = Math.Max(settings.DowntimeHeatmapMax, settings.DowntimeHeatmapMin + 1);

                    var selected = vm.HeatmapSettingOptions.FirstOrDefault(v => string.Equals(v, settings.HeatmapSelectedSetting, StringComparison.CurrentCultureIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(selected))
                        vm.SelectedHeatmapSetting = selected;

                    // Load UI State Filters (so they are active at startup/reload, but we still Restore them properly after VM loads data)
                    vm.SelectedIssueTypeFilter = settings.SelectedIssueTypeFilter ?? "Все позиции";
                    vm.SelectedDowntimeIssueTypeFilter = settings.SelectedDowntimeIssueTypeFilter ?? "Все типы";
                    vm.SelectedDowntimeStatusFilter = settings.SelectedDowntimeStatusFilter ?? "Все статусы";
                    vm.SelectedDowntimeResponsibleFilter = settings.SelectedDowntimeResponsibleFilter ?? "Все ответственные";
                    vm.SelectedDowntimeSubdivisionFilter = settings.SelectedDowntimeSubdivisionFilter ?? "Все группы";
                    vm.DowntimeEquipmentSearchQuery = settings.DowntimeEquipmentSearchQuery ?? string.Empty;
                    vm.SelectedEmployeeTimelineEmployee = settings.SelectedEmployeeTimelineEmployee ?? "Все сотрудники";

                    if (vm.Dashboard != null)
                    {
                        vm.Dashboard.SelectedDashboardIssueTypeFilter = settings.SelectedDashboardIssueTypeFilter ?? "Все типы";
                        vm.Dashboard.SelectedDashboardResponsibleFilter = settings.SelectedDashboardResponsibleFilter ?? "Все ответственные";
                        vm.Dashboard.SelectedDashboardSubdivisionFilter = settings.SelectedDashboardSubdivisionFilter ?? "Все группы";
                    }

                    _savedDowntimeIssueTypeFilter = vm.SelectedDowntimeIssueTypeFilter;
                    _savedDowntimeStatusFilter = vm.SelectedDowntimeStatusFilter;
                    _savedDowntimeResponsibleFilter = vm.SelectedDowntimeResponsibleFilter;
                    _savedDowntimeSubdivisionFilter = vm.SelectedDowntimeSubdivisionFilter;
                    _savedDowntimeEquipmentSearchQuery = vm.DowntimeEquipmentSearchQuery;
                    _savedDashboardSubdivisionFilter = vm.Dashboard?.SelectedDashboardSubdivisionFilter ?? "Все группы";
                }

                if (DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel reportOwnerVm)
                {
                    reportOwnerVm.Reports.ReportStartDate = settings.ReportStartDate ?? DateTime.Now.Date;
                    reportOwnerVm.Reports.ReportEndDate = settings.ReportEndDate ?? DateTime.Now.Date;
                    reportOwnerVm.Reports.ReportGroupByKey = NormalizeReportGroupByKey(settings.ReportGroupBy);
                    reportOwnerVm.Reports.ReportIncludeDashboard = settings.ReportIncludeDashboard;
                    reportOwnerVm.Reports.ReportIncludeDowntime = settings.ReportIncludeDowntime;
                    reportOwnerVm.Reports.ReportIncludeEmployee = settings.ReportIncludeEmployee;
                    reportOwnerVm.Reports.ReportOpenAfterGenerate = settings.ReportOpenAfterGenerate;
                    reportOwnerVm.Reports.ReportOnlyInProgress = settings.ReportOnlyInProgress;
                    reportOwnerVm.Reports.ReportFilterByDuration = settings.ReportFilterByDuration;
                    reportOwnerVm.Reports.ReportMinDurationMinutes = Math.Max(0, settings.ReportMinDurationMinutes);
                    reportOwnerVm.Reports.ReportFieldStart = settings.ReportFieldStart;
                    reportOwnerVm.Reports.ReportFieldEnd = settings.ReportFieldEnd;
                    reportOwnerVm.Reports.ReportFieldEquipment = settings.ReportFieldEquipment;
                    reportOwnerVm.Reports.ReportFieldSubdivision = settings.ReportFieldSubdivision;
                    reportOwnerVm.Reports.ReportFieldType = settings.ReportFieldType;
                    reportOwnerVm.Reports.ReportFieldResponsible = settings.ReportFieldResponsible;
                    reportOwnerVm.Reports.ReportFieldDescription = settings.ReportFieldDescription;
                    reportOwnerVm.Reports.ReportLastFilePath = settings.ReportLastFilePath ?? string.Empty;
                }
            }
            catch
            {
                // ignore invalid settings file
            }
            finally
            {
                _suppressSettingsSave = false;
                ConfigureJiraAutoImportLoop();
            }
        }

        public void RestoreActiveUiState()
        {
            try
            {
                _suppressSettingsSave = true;

                if (!_jiraSettingsStore.TryLoad("jira_import_settings.json", out JiraImportSettings? settings) || settings == null)
                    return;

                if (DataContext is EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
                {
                    // 1. Restore filters
                    vm.SelectedIssueTypeFilter = settings.SelectedIssueTypeFilter ?? "Все позиции";
                    vm.SelectedDowntimeIssueTypeFilter = settings.SelectedDowntimeIssueTypeFilter ?? "Все типы";
                    vm.SelectedDowntimeStatusFilter = settings.SelectedDowntimeStatusFilter ?? "Все статусы";
                    vm.SelectedDowntimeResponsibleFilter = settings.SelectedDowntimeResponsibleFilter ?? "Все ответственные";
                    vm.SelectedDowntimeSubdivisionFilter = settings.SelectedDowntimeSubdivisionFilter ?? "Все группы";
                    vm.DowntimeEquipmentSearchQuery = settings.DowntimeEquipmentSearchQuery ?? string.Empty;
                    vm.SelectedEmployeeTimelineEmployee = settings.SelectedEmployeeTimelineEmployee ?? "Все сотрудники";

                    if (vm.Dashboard != null)
                    {
                        vm.Dashboard.SelectedDashboardIssueTypeFilter = settings.SelectedDashboardIssueTypeFilter ?? "Все типы";
                        vm.Dashboard.SelectedDashboardResponsibleFilter = settings.SelectedDashboardResponsibleFilter ?? "Все ответственные";
                        vm.Dashboard.SelectedDashboardSubdivisionFilter = settings.SelectedDashboardSubdivisionFilter ?? "Все группы";
                    }

                    _savedDowntimeIssueTypeFilter = vm.SelectedDowntimeIssueTypeFilter;
                    _savedDowntimeStatusFilter = vm.SelectedDowntimeStatusFilter;
                    _savedDowntimeResponsibleFilter = vm.SelectedDowntimeResponsibleFilter;
                    _savedDowntimeSubdivisionFilter = vm.SelectedDowntimeSubdivisionFilter;
                    _savedDowntimeEquipmentSearchQuery = vm.DowntimeEquipmentSearchQuery;
                    _savedDashboardSubdivisionFilter = vm.Dashboard?.SelectedDashboardSubdivisionFilter ?? "Все группы";

                    // 2. Restore selected equipment
                    if (settings.SelectedEquipmentUid.HasValue)
                    {
                        var foundEq = vm.EquipmentCollection.FirstOrDefault(e => e.Uid == settings.SelectedEquipmentUid.Value);
                        if (foundEq != null)
                        {
                            vm.LoadEquipmentCommand.Execute(foundEq).Subscribe(_ =>
                            {
                                if (vm.ShowDayTimelineCommand != null)
                                {
                                    vm.ShowDayTimelineCommand.Execute(DateTime.Today).Subscribe();
                                }
                            });
                        }
                    }

                    // 3. Set analysis dates to the current day (today)
                    vm.AnalysisDate = DateTime.Today;
                    vm.DowntimeAnalysisDate = DateTime.Today;
                    vm.EmployeeTimelineDate = DateTime.Today;

                    if (vm.Downtime.ShowDowntimeDayCommand != null)
                    {
                        vm.Downtime.ShowDowntimeDayCommand.Execute(DateTime.Today).Subscribe();
                    }

                    // 4. Restore active page
                    if (Enum.TryParse<AppPage>(settings.LastActivePage, out var parsedPage))
                    {
                        NavigateToPage(parsedPage);
                    }
                }
            }
            catch
            {
                // ignore
            }
            finally
            {
                _suppressSettingsSave = false;
            }
        }

        private async Task<(string Url, string Username, string Token, string Jql, IReadOnlyCollection<string> FilterIds)> GetJiraApiSettingsFromUiAsync()
        {
            return await Dispatcher.UIThread.InvokeAsync(GetJiraApiSettingsFromUi);
        }

        private void LogImportError(string contextMessage, Exception? ex)
        {
            try
            {
                var appFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "EquipmentFailureAnalysis");
                Directory.CreateDirectory(appFolder);
                var logFile = Path.Combine(appFolder, "import_errors.log");
                
                var logEntry = new StringBuilder();
                logEntry.AppendLine($"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] {contextMessage}");
                if (ex != null)
                {
                    logEntry.AppendLine($"Type: {ex.GetType().FullName}");
                    logEntry.AppendLine($"Message: {ex.Message}");
                    logEntry.AppendLine($"StackTrace:\n{ex.StackTrace}");
                }
                logEntry.AppendLine(new string('-', 80));
                
                File.AppendAllText(logFile, logEntry.ToString(), Encoding.UTF8);

                if (File.Exists(logFile))
                {
                    var lines = File.ReadAllLines(logFile);
                    if (lines.Length > 2000)
                    {
                        var truncated = lines.Skip(lines.Length - 1000).ToArray();
                        File.WriteAllLines(logFile, truncated, Encoding.UTF8);
                    }
                }
            }
            catch
            {
                // ignore logging errors
            }
        }

        private async Task<bool> RunJiraImportAsync(bool showDialogs, string sourceLabel, bool isAutoImport, TimeSpan? autoInterval, CancellationToken cancellationToken = default)
        {
            var (url, username, token, jql, filterIds) = await GetJiraApiSettingsFromUiAsync();
            if (string.IsNullOrWhiteSpace(url))
            {
                const string message = "Укажите URL Jira API перед импортом.";
                if (showDialogs)
                    await ShowMessageAsync("Ошибка импорта Jira", message);
                PublishStatus($"Ошибка импорта Jira: {message}");
                return false;
            }

            var vm = this.DataContext as EquipmentFailureAnalysis.ViewModels.MainWindowViewModel;
            if (vm != null)
            {
                vm.IsLoading = true;
            }

            await _jiraImportLock.WaitAsync(cancellationToken);
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                var result = await _jiraImportService.ImportAsync(new JiraImportRequest
                {
                    Url = url,
                    Username = username,
                    Token = token,
                    Jql = jql,
                    FilterIds = filterIds,
                    PageSize = 1000,
                    TotalResultsLimit = null,
                    EnsureLatestOrdering = false
                }, cancellationToken);

                if (!result.Success)
                {
                    if (showDialogs)
                    {
                        var title = string.IsNullOrWhiteSpace(url) ? "Ошибка" : "Ошибка импорта Jira";
                        await ShowMessageAsync(title, result.ErrorMessage);
                    }
                    PublishStatus($"Ошибка импорта Jira: {result.ErrorMessage}");
                    LogImportError($"[JIRA IMPORT FAIL] {result.ErrorMessage}", null);
                    return false;
                }

                await Dispatcher.UIThread.InvokeAsync(() => ApplyParsedResults(result.Items));
                if (showDialogs)
                    await ShowMessageAsync("Импорт из Jira", result.BuildSummaryMessage());

                _lastSuccessfulJiraImportUtc = DateTime.UtcNow;
                await Dispatcher.UIThread.InvokeAsync(() => SaveJiraSettingsFromUi(immediate: true));

                PublishStatus($"{sourceLabel} (полный): {result.Items.Count} ед. оборудования, {result.IssuesCount} событий.");
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                PublishStatus($"Ошибка импорта Jira: {ex.Message}");
                LogImportError($"[JIRA IMPORT EXCEPTION] {ex.Message}", ex);
                return false;
            }
            finally
            {
                if (vm != null)
                {
                    vm.IsLoading = false;
                }
                _jiraImportLock.Release();
            }
        }

        private void ConfigureJiraAutoImportLoop()
        {
            if (_suppressSettingsSave)
                return;

            if (DataContext is not EquipmentFailureAnalysis.ViewModels.MainWindowViewModel vm)
            {
                StopJiraAutoImportLoop();
                return;
            }

            var settings = vm.Settings;
            if (!settings.JiraAutoImportEnabled)
            {
                StopJiraAutoImportLoop();
                return;
            }

            StartJiraAutoImportLoop(TimeSpan.FromMinutes(Math.Clamp(settings.JiraAutoImportPeriodMinutes, 1, 1440)));
        }

        private void StopJiraAutoImportLoop()
        {
            if (_jiraAutoImportCts == null)
                return;

            try
            {
                _jiraAutoImportCts.Cancel();
                _jiraAutoImportCts.Dispose();
            }
            catch
            {
                // ignore cancellation errors
            }
            finally
            {
                _jiraAutoImportCts = null;
                _jiraAutoImportTask = null;
            }
        }

        private void StartJiraAutoImportLoop(TimeSpan interval)
        {
            StopJiraAutoImportLoop();

            _jiraAutoImportCts = new CancellationTokenSource();
            var token = _jiraAutoImportCts.Token;

            _jiraAutoImportTask = Task.Run(async () =>
            {
                PublishStatus($"Фоновый импорт Jira включен (интервал: {interval.TotalMinutes:0} мин).");

                while (!token.IsCancellationRequested)
                {
                    await RunJiraImportAsync(showDialogs: false, sourceLabel: "Фоновый импорт Jira", isAutoImport: true, autoInterval: interval, cancellationToken: token);
                    try
                    {
                        await Task.Delay(interval, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, token);
        }

    }
}
