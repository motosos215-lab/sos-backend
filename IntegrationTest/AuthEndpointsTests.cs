using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class AuthEndpointsTests
{
    [Fact]
    public async Task RegisterReturnsCreatedWithUserData()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        HttpClient client = factory.CreateClient();
        var request = new RegisterRequest("rider@example.com", "StrongPass1!", "Moto Rider", null);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        content.Should().Contain("rider@example.com");
        content.Should().NotContain("PasswordHash");
        content.Should().NotContain("StrongPass1!");
    }

    [Fact]
    public async Task LoginReturnsAccessAndRefreshTokens()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        HttpClient client = factory.CreateClient();
        var register = new RegisterRequest("login@example.com", "StrongPass1!", "Login Rider", null);
        await client.PostAsJsonAsync("/api/v1/auth/register", register);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Email, register.Password));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("accessToken");
        content.Should().Contain("refreshToken");
        content.Should().NotContain("PasswordHash");
    }

    [Fact]
    public async Task CurrentUserRequiresAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "MotoSOS",
                    ["Jwt:Audience"] = "MotoSOS.Clients",
                    ["Jwt:Key"] = new string('I', 48),
                    ["Jwt:AccessTokenMinutes"] = "15",
                    ["Jwt:RefreshTokenDays"] = "7"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IUserRepository, InMemoryUserRepository>();
                services.AddSingleton<IRefreshTokenRepository, InMemoryRefreshTokenRepository>();
            });
        });
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly List<User> _users = [];

        public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(_users.FirstOrDefault(user => user.Id == id));

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(_users.FirstOrDefault(user => string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            _users.Add(user);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly List<RefreshToken> _tokens = [];

        public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(_tokens.FirstOrDefault(token => token.TokenHash == tokenHash));

        public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
        {
            _tokens.Add(refreshToken);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
