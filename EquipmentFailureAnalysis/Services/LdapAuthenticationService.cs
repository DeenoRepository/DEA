using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Linq;
using System.Net;

namespace EquipmentFailureAnalysis.Services
{
    public sealed class LdapAuthenticationRequest
    {
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool UseSsl { get; set; }
        public string Domain { get; set; } = string.Empty;
        public string BaseDn { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class LdapAuthenticationResult
    {
        public bool Success { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
    }

    public sealed class LdapAuthenticationService
    {
        public LdapAuthenticationResult Authenticate(LdapAuthenticationRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Server))
                return Fail("Не указан LDAP-сервер.");
            if (string.IsNullOrWhiteSpace(request.Username))
                return Fail("Не указан логин пользователя.");
            if (string.IsNullOrEmpty(request.Password))
                return Fail("Не указан пароль пользователя.");

            var port = request.Port > 0 ? request.Port : (request.UseSsl ? 636 : 389);
            var identifier = new LdapDirectoryIdentifier(request.Server.Trim(), port, false, false);
            var errors = new List<string>();

            foreach (var credential in BuildCredentials(request.Username, request.Password, request.Domain))
            {
                try
                {
                    using var connection = new LdapConnection(identifier)
                    {
                        AuthType = AuthType.Negotiate,
                        Timeout = TimeSpan.FromSeconds(10),
                        Credential = credential
                    };

                    connection.SessionOptions.ProtocolVersion = 3;
                    connection.SessionOptions.SecureSocketLayer = request.UseSsl;
                    connection.Bind();

                    if (!string.IsNullOrWhiteSpace(request.BaseDn))
                    {
                        var searchRequest = new SearchRequest(
                            request.BaseDn.Trim(),
                            "(objectClass=*)",
                            SearchScope.Base,
                            "distinguishedName");
                        _ = connection.SendRequest(searchRequest);
                    }

                    return new LdapAuthenticationResult { Success = true };
                }
                catch (LdapException ex)
                {
                    var details = ex.ServerErrorMessage;
                    errors.Add(string.IsNullOrWhiteSpace(details) ? ex.Message : details);
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                }
            }

            var error = errors.FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                ?? "LDAP-аутентификация завершилась неуспешно.";
            return Fail(error);
        }

        private static IEnumerable<NetworkCredential> BuildCredentials(string username, string password, string domain)
        {
            var login = username.Trim();
            if (string.IsNullOrWhiteSpace(login))
                yield break;

            var normalizedDomain = domain?.Trim() ?? string.Empty;
            var hasExplicitDomain = login.Contains("@", StringComparison.Ordinal) || login.Contains("\\", StringComparison.Ordinal);

            var produced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool Register(NetworkCredential credential)
            {
                var key = $"{credential.Domain}|{credential.UserName}";
                if (produced.Contains(key))
                    return false;
                produced.Add(key);
                return true;
            }

            if (hasExplicitDomain)
            {
                var explicitCredential = new NetworkCredential(login, password);
                if (Register(explicitCredential))
                    yield return explicitCredential;
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(normalizedDomain))
            {
                var withDomain = new NetworkCredential(login, password, normalizedDomain);
                if (Register(withDomain))
                    yield return withDomain;

                var samAccountName = new NetworkCredential($@"{normalizedDomain}\{login}", password);
                if (Register(samAccountName))
                    yield return samAccountName;

                var upn = new NetworkCredential($"{login}@{normalizedDomain}", password);
                if (Register(upn))
                    yield return upn;
            }

            var plain = new NetworkCredential(login, password);
            if (Register(plain))
                yield return plain;
        }

        private static LdapAuthenticationResult Fail(string message) => new LdapAuthenticationResult
        {
            Success = false,
            ErrorMessage = message
        };
    }
}
