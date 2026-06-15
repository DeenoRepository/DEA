using System;
using EquipmentFailureAnalysis.Services;
using Xunit;

namespace EquipmentFailureAnalysis.Tests
{
    public class LdapAuthenticationServiceTests
    {
        [Fact]
        public void Authenticate_ShouldFail_WhenServerIsEmpty()
        {
            // Arrange
            var service = new LdapAuthenticationService();
            var request = new LdapAuthenticationRequest
            {
                Server = "",
                Username = "testuser",
                Password = "password"
            };

            // Act
            var result = service.Authenticate(request);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Не указан LDAP-сервер.", result.ErrorMessage);
        }

        [Fact]
        public void Authenticate_ShouldFail_WhenUsernameIsEmpty()
        {
            // Arrange
            var service = new LdapAuthenticationService();
            var request = new LdapAuthenticationRequest
            {
                Server = "ldap.test.local",
                Username = "",
                Password = "password"
            };

            // Act
            var result = service.Authenticate(request);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Не указан логин пользователя.", result.ErrorMessage);
        }

        [Fact]
        public void Authenticate_ShouldFail_WhenPasswordIsEmpty()
        {
            // Arrange
            var service = new LdapAuthenticationService();
            var request = new LdapAuthenticationRequest
            {
                Server = "ldap.test.local",
                Username = "testuser",
                Password = ""
            };

            // Act
            var result = service.Authenticate(request);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Не указан пароль пользователя.", result.ErrorMessage);
        }

        [Fact]
        public void Authenticate_ShouldFail_WhenConnectionFails()
        {
            // Arrange
            var service = new LdapAuthenticationService();
            var request = new LdapAuthenticationRequest
            {
                Server = "nonexistent.ldap.server.local",
                Username = "testuser",
                Password = "password",
                Port = 389,
                UseSsl = false
            };

            // Act
            var result = service.Authenticate(request);

            // Assert
            Assert.False(result.Success);
            Assert.NotEmpty(result.ErrorMessage);
        }
    }
}
