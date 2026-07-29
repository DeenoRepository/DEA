using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using EquipmentFailureAnalysis.Services;
using ReactiveUI;
using System;
using System.Threading.Tasks;

namespace EquipmentFailureAnalysis.Views
{
    public sealed class LdapLoginRequest
    {
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool UseSsl { get; set; }
        public string Domain { get; set; } = string.Empty;
        public string BaseDn { get; set; } = string.Empty;
        public string InitialUsername { get; set; } = string.Empty;
    }

    public sealed class LdapLoginResult
    {
        public bool Success { get; init; }
        public string Username { get; init; } = string.Empty;
    }

    public partial class LdapLoginWindow : Window
    {
        private sealed class LdapLoginWindowViewModel : ReactiveObject
        {
            private string _username = string.Empty;
            private string _password = string.Empty;
            private string _statusText = "Введите учетные данные LDAP.";

            public string Username
            {
                get => _username;
                set => this.RaiseAndSetIfChanged(ref _username, value ?? string.Empty);
            }

            public string Password
            {
                get => _password;
                set => this.RaiseAndSetIfChanged(ref _password, value ?? string.Empty);
            }

            public string StatusText
            {
                get => _statusText;
                set => this.RaiseAndSetIfChanged(ref _statusText, value ?? string.Empty);
            }
        }

        private readonly LdapLoginRequest _request;
        private readonly LdapAuthenticationService _authenticationService;
        private readonly LdapLoginWindowViewModel _viewModel;
        private readonly TextBlock _serverText;
        private readonly TextBlock _errorText;
        private readonly TextBox _usernameBox;
        private bool _isAuthenticating;

        public LdapLoginWindow()
            : this(new LdapLoginRequest
            {
                Server = "localhost",
                Port = 389
            })
        {
        }

        public LdapLoginWindow(LdapLoginRequest request, LdapAuthenticationService? authenticationService = null)
        {
            _request = request;
            _authenticationService = authenticationService ?? new LdapAuthenticationService();
            _viewModel = new LdapLoginWindowViewModel
            {
                Username = request.InitialUsername?.Trim() ?? string.Empty
            };

            DataContext = _viewModel;
            InitializeComponent();

            _serverText = this.FindControl<TextBlock>("ServerText")
                ?? throw new InvalidOperationException("ServerText not found.");
            _errorText = this.FindControl<TextBlock>("ErrorText")
                ?? throw new InvalidOperationException("ErrorText not found.");
            _usernameBox = this.FindControl<TextBox>("UsernameBox")
                ?? throw new InvalidOperationException("UsernameBox not found.");

            UpdateServerText();

            Opened += (_, _) =>
            {
                _usernameBox.Focus();
                _usernameBox.CaretIndex = _usernameBox.Text?.Length ?? 0;
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void UpdateServerText()
        {
            var port = _request.Port > 0 ? _request.Port : (_request.UseSsl ? 636 : 389);
            var protocol = _request.UseSsl ? "LDAPS" : "LDAP";
            var domainSuffix = string.IsNullOrWhiteSpace(_request.Domain) ? string.Empty : $", домен: {_request.Domain.Trim()}";
            _serverText.Text = $"{protocol} {_request.Server}:{port}{domainSuffix}";
        }

        private async void SignInButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_isAuthenticating)
                return;

            HideError();
            var username = _viewModel.Username?.Trim() ?? string.Empty;
            var password = _viewModel.Password ?? string.Empty;

            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("Введите логин LDAP.");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowError("Введите пароль LDAP.");
                return;
            }

            _isAuthenticating = true;
            _viewModel.StatusText = "Проверяем учетные данные...";

            LdapAuthenticationResult result;
            try
            {
                result = await Task.Run(() => _authenticationService.Authenticate(new LdapAuthenticationRequest
                {
                    Server = _request.Server,
                    Port = _request.Port > 0 ? _request.Port : (_request.UseSsl ? 636 : 389),
                    UseSsl = _request.UseSsl,
                    Domain = _request.Domain,
                    BaseDn = _request.BaseDn,
                    Username = username,
                    Password = password
                }));
            }
            catch (Exception ex)
            {
                result = new LdapAuthenticationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
            finally
            {
                _isAuthenticating = false;
            }

            if (!result.Success)
            {
                _viewModel.StatusText = "Авторизация не выполнена.";
                ShowError(string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Не удалось проверить учетные данные LDAP."
                    : result.ErrorMessage);
                return;
            }

            _viewModel.StatusText = "Авторизация выполнена успешно.";
            Close(new LdapLoginResult
            {
                Success = true,
                Username = username
            });
        }

        private async void SettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            var settingsDialog = new LdapConnectionSettingsWindow(new LdapConnectionSettings
            {
                Server = _request.Server,
                Port = _request.Port > 0 ? _request.Port : (_request.UseSsl ? 636 : 389),
                UseSsl = _request.UseSsl,
                Domain = _request.Domain,
                BaseDn = _request.BaseDn
            });

            var result = await settingsDialog.ShowDialog<LdapConnectionSettingsResult?>(this);
            if (result?.Accepted != true || result.Settings == null)
                return;

            _request.Server = result.Settings.Server?.Trim() ?? string.Empty;
            _request.Port = result.Settings.Port > 0 ? result.Settings.Port : 389;
            _request.UseSsl = result.Settings.UseSsl;
            _request.Domain = result.Settings.Domain?.Trim() ?? string.Empty;
            _request.BaseDn = result.Settings.BaseDn?.Trim() ?? string.Empty;

            UpdateServerText();
            HideError();
            _viewModel.StatusText = "Параметры LDAP обновлены.";
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Close(new LdapLoginResult { Success = false });
        }

        private void ShowError(string message)
        {
            _errorText.Text = message;
            _errorText.IsVisible = true;
        }

        private void TogglePasswordVisibility_Click(object? sender, RoutedEventArgs e)
        {
            if (this.FindControl<Avalonia.Controls.TextBox>("PasswordBox") is { } box &&
                this.FindControl<Avalonia.Controls.TextBlock>("PasswordRevealGlyph") is { } glyph)
            {
                if (box.PasswordChar == '\0')
                {
                    box.PasswordChar = '*';
                    glyph.Text = "\uE7B3"; // Eye (closed)
                }
                else
                {
                    box.PasswordChar = '\0';
                    glyph.Text = "\uE7B3".Length > 0 ? "\uED1A" : "\uE7B3"; // Eye open
                    glyph.Text = "\uED1A";
                }
            }
        }

        private void HideError()
        {
            _errorText.Text = string.Empty;
            _errorText.IsVisible = false;
        }
    }
}
