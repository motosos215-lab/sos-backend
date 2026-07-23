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

namespace SecurityTest;

public sealed class AuthSecurityTests
{
    [Fact]
    public async Task RegisterResponseDoesNotExposePasswordHash()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            CreateRegisterRequest("safe@example.com"));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        content.Should().NotContain("PasswordHash");
        content.Should().NotContain("StrongPass1!");
    }

    [Fact]
    public async Task LoginWithUnknownUserReturnsGenericUnauthorizedMessage()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("missing@example.com", "StrongPass1!"));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        content.Should().Contain("Invalid authentication credentials.");
        content.Should().Contain("invalid_credentials");
        content.Should().NotContain("missing@example.com");
    }

    [Fact]
    public async Task CurrentUserRejectsAnonymousRequests()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterRejectsWeakPassword()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest("weak@example.com", "password", "password", "Weak Rider", null, "Rider", true));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefreshTokenIsStoredHashed()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("refresh@example.com");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Email, register.Password));
        LoginEnvelope? login = await response.Content.ReadFromJsonAsync<LoginEnvelope>();

        login.Should().NotBeNull();
        stores.RefreshTokens.Tokens.Should().ContainSingle();
        stores.RefreshTokens.Tokens[0].TokenHash.Should().NotBe(login!.Data.RefreshToken);
    }

    [Fact]
    public async Task LoginWithCodeDoesNotAcceptHardcodedCode()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login-with-code",
            new LoginWithCodeRequest("safe@example.com", "123456"));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
        content.Should().Contain("feature_not_implemented");
        content.Should().NotContain("accessToken");
        content.Should().NotContain("123456");
    }

    [Fact]
    public async Task ForgotPasswordDoesNotReturnResetTokens()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password",
            new ForgotPasswordRequest("safe@example.com"));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        content.Should().BeEmpty();
    }

    private static RegisterRequest CreateRegisterRequest(string email)
    {
        return new RegisterRequest(email, "StrongPass1!", "StrongPass1!", "Safe Rider", null, "Rider", true);
    }

    private static WebApplicationFactory<Program> CreateFactory(TestStores stores)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "MotoSOS",
                    ["Jwt:Audience"] = "MotoSOS.Clients",
                    ["Jwt:Key"] = new string('S', 48),
                    ["Jwt:AccessTokenMinutes"] = "15",
                    ["Jwt:RefreshTokenDays"] = "7",
                    ["Jwt:RefreshTokenRememberMeDays"] = "30"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IUserRepository>(stores.Users);
                services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens);
            });
        });
    }

    private sealed class TestStores
    {
        public InMemoryUserRepository Users { get; } = new();

        public InMemoryRefreshTokenRepository RefreshTokens { get; } = new();
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
        public List<RefreshToken> Tokens { get; } = [];

        public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult(Tokens.FirstOrDefault(token => token.TokenHash == tokenHash));

        public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
        {
            Tokens.Add(refreshToken);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
}
