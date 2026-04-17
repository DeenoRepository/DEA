using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;

namespace EquipmentFailureAnalysis.Views
{
    public sealed class LdapConnectionSettings
    {
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; } = 389;
        public bool UseSsl { get; set; }
        public string Domain { get; set; } = string.Empty;
        public string BaseDn { get; set; } = string.Empty;
    }

    public sealed class LdapConnectionSettingsResult
    {
        public bool Accepted { get; init; }
        public LdapConnectionSettings Settings { get; init; } = new();
    }

    public partial class LdapConnectionSettingsWindow : Window
    {
        private readonly TextBox _serverBox;
        private readonly NumericUpDown _portBox;
        private readonly CheckBox _useSslBox;
        private readonly TextBox _domainBox;
        private readonly TextBox _baseDnBox;

        public LdapConnectionSettingsWindow()
            : this(new LdapConnectionSettings())
        {
        }

        public LdapConnectionSettingsWindow(LdapConnectionSettings settings)
        {
            InitializeComponent();

            _serverBox = this.FindControl<TextBox>("ServerBox")
                ?? throw new InvalidOperationException("ServerBox not found.");
            _portBox = this.FindControl<NumericUpDown>("PortBox")
                ?? throw new InvalidOperationException("PortBox not found.");
            _useSslBox = this.FindControl<CheckBox>("UseSslBox")
                ?? throw new InvalidOperationException("UseSslBox not found.");
            _domainBox = this.FindControl<TextBox>("DomainBox")
                ?? throw new InvalidOperationException("DomainBox not found.");
            _baseDnBox = this.FindControl<TextBox>("BaseDnBox")
                ?? throw new InvalidOperationException("BaseDnBox not found.");

            var port = settings.Port > 0 ? settings.Port : (settings.UseSsl ? 636 : 389);
            _serverBox.Text = settings.Server ?? string.Empty;
            _portBox.Value = port;
            _useSslBox.IsChecked = settings.UseSsl;
            _domainBox.Text = settings.Domain ?? string.Empty;
            _baseDnBox.Text = settings.BaseDn ?? string.Empty;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void ApplyButton_Click(object? sender, RoutedEventArgs e)
        {
            var settings = new LdapConnectionSettings
            {
                Server = _serverBox.Text?.Trim() ?? string.Empty,
                Port = (int)Math.Clamp((int)Math.Round(_portBox.Value ?? 389), 1, 65535),
                UseSsl = _useSslBox.IsChecked == true,
                Domain = _domainBox.Text?.Trim() ?? string.Empty,
                BaseDn = _baseDnBox.Text?.Trim() ?? string.Empty
            };

            Close(new LdapConnectionSettingsResult
            {
                Accepted = true,
                Settings = settings
            });
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(new LdapConnectionSettingsResult
            {
                Accepted = false,
                Settings = new LdapConnectionSettings()
            });
        }
    }
}
