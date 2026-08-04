using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
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
    public async Task RegisterRiderReturnsCreatedWithUserData()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var request = CreateRegisterRequest("rider@example.com", "Rider");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        content.Should().Contain("rider@example.com");
        content.Should().Contain("Rider");
        content.Should().NotContain("PasswordHash");
        content.Should().NotContain("StrongPass1!");
        stores.Users.Users.Where(user => user.Role == UserRole.Rider && user.AcceptedTermsAtUtc.HasValue).Should().ContainSingle();
    }

    [Fact]
    public async Task RegisterMonitorReturnsCreatedWithMonitorRole()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var request = CreateRegisterRequest("monitor@example.com", "Monitor");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        stores.Users.Users.Should().ContainSingle(user => user.Email == "monitor@example.com" && user.Role == UserRole.Monitor);
    }

    [Fact]
    public async Task RegisterConductorMapsToRiderRole()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var request = CreateRegisterRequest("conductor@example.com", "Conductor");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        stores.Users.Users.Should().ContainSingle(user => user.Email == "conductor@example.com" && user.Role == UserRole.Rider);
    }

    [Fact]
    public async Task RegisterWithoutAcceptedTermsReturnsTermsNotAccepted()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var request = CreateRegisterRequest("terms@example.com", "Rider") with { AcceptTerms = false };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        content.Should().Contain("terms_not_accepted");
    }

    [Fact]
    public async Task RegisterWithDifferentPasswordsReturnsBadRequest()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var request = CreateRegisterRequest("mismatch@example.com", "Rider") with { ConfirmPassword = "Different1!" };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RegisterDuplicateReturnsConflict()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var request = CreateRegisterRequest("duplicate@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", request);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        content.Should().Contain("user_already_exists");
    }

    [Fact]
    public async Task LoginReturnsAccessAndRefreshTokens()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("login@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Email, register.Password));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("accessToken");
        content.Should().Contain("refreshToken");
        content.Should().NotContain("PasswordHash");
    }

    [Fact]
    public async Task LoginWithRememberMeExtendsOnlyRefreshTokenExpiration()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("remember@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Email, register.Password, true));
        LoginEnvelope? login = await response.Content.ReadFromJsonAsync<LoginEnvelope>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        login.Should().NotBeNull();
        stores.RefreshTokens.Tokens.Should().ContainSingle();
        stores.RefreshTokens.Tokens[0].ExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(20));
        login!.Data.AccessTokenExpiresAtUtc.Should().BeBefore(DateTimeOffset.UtcNow.AddHours(1));
    }

    [Fact]
    public async Task LoginWithInvalidCredentialsReturnsUnauthorized()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("missing@example.com", "StrongPass1!"));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        content.Should().Contain("invalid_credentials");
        content.Should().NotContain("missing@example.com");
    }

    [Fact]
    public async Task ForgotPasswordReturnsNoContentForExistingUser()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("forgot-existing@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest(register.Email));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        content.Should().BeEmpty();
    }

    [Fact]
    public async Task ForgotPasswordReturnsNoContentForMissingUser()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new ForgotPasswordRequest("missing@example.com"));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        content.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestAccessCodeReturnsNoContentForExistingUser()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("code-existing@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/request-access-code", new RequestAccessCodeRequest(register.Email));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RequestAccessCodeReturnsNoContentForMissingUser()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/request-access-code", new RequestAccessCodeRequest("missing-code@example.com"));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task LoginWithCodeReturnsFeatureNotImplemented()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/login-with-code", new LoginWithCodeRequest("code@example.com", "123456"));
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
        content.Should().Contain("feature_not_implemented");
        content.Should().NotContain("123456");
    }

    [Fact]
    public async Task CurrentUserReturnsUserWithValidToken()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        var register = CreateRegisterRequest("me@example.com", "Rider");
        await client.PostAsJsonAsync("/api/v1/auth/register", register);
        HttpResponseMessage loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Email, register.Password));
        LoginEnvelope? login = await loginResponse.Content.ReadFromJsonAsync<LoginEnvelope>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Data.AccessToken);

        HttpResponseMessage response = await client.GetAsync("/api/v1/users/me");
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("me@example.com");
        content.Should().NotContain("PasswordHash");
    }

    [Fact]
    public async Task CurrentUserRequiresAuthentication()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static RegisterRequest CreateRegisterRequest(string email, string accountType)
    {
        return new RegisterRequest(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555 555 5555", accountType, true);
    }

    private static WebApplicationFactory<Program> CreateFactory(TestStores stores)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "MotoSOS",
                    ["Jwt:Audience"] = "MotoSOS.Clients",
                    ["Jwt:Key"] = new string('I', 48),
                    ["Jwt:AccessTokenMinutes"] = "15",
                    ["Jwt:RefreshTokenDays"] = "7",
                    ["Jwt:RefreshTokenRememberMeDays"] = "30",
                    ["MongoDb:ConnectionString"] = string.Empty,
                    ["MongoDb:DatabaseName"] = "MotoSOS_Test"
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
        public List<User> Users { get; } = [];

        public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(user => user.Id == id));

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(Users.FirstOrDefault(user => string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            Users.Add(user);
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

    private sealed record LoginEnvelope(bool Success, LoginData Data);

    private sealed record LoginData(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAtUtc, AuthUserResponse User);
}
