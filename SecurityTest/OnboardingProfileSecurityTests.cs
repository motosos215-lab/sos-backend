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
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace SecurityTest;

public sealed class OnboardingProfileSecurityTests
{
    [Fact]
    public async Task ProfileDoesNotExposePasswordHashOrRefreshToken()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "profile-safe@example.com", stores);

        HttpResponseMessage response = await client.GetAsync("/api/v1/profiles/me");
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotContain("PasswordHash");
        content.Should().NotContain("passwordHash");
        content.Should().NotContain("refreshToken");
    }

    [Fact]
    public async Task OnboardingDoesNotExposePasswordHashOrRefreshToken()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "onboarding-safe@example.com", stores);

        HttpResponseMessage response = await client.GetAsync("/api/v1/onboarding/status");
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotContain("PasswordHash");
        content.Should().NotContain("passwordHash");
        content.Should().NotContain("refreshToken");
    }

    [Fact]
    public async Task ProfileEndpointsUseAuthenticatedUserOnly()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient firstClient = factory.CreateClient();
        HttpClient secondClient = factory.CreateClient();
        await AuthenticateAsync(firstClient, "first@example.com", stores);
        await firstClient.PutAsJsonAsync("/api/v1/profiles/me", ValidContinuePayload("First Rider"));
        await AuthenticateAsync(secondClient, "second@example.com", stores);

        HttpResponseMessage response = await secondClient.GetAsync("/api/v1/profiles/me");
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("second@example.com");
        content.Should().NotContain("first@example.com");
        content.Should().NotContain("First Rider");
    }

    [Fact]
    public async Task ProfileDoesNotAllowChangingEmailRoleOrActiveStateFromUnexpectedFields()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "immutable@example.com", stores);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/profiles/me",
            new
            {
                fullName = "Changed Name",
                phoneNumber = "+52 555 555 5555",
                dateOfBirth = "1995-01-15",
                addressOrZone = "Centro",
                primaryCity = "Toluca",
                provisionalEmergencyContactName = "Contacto",
                provisionalEmergencyContactPhone = "+52 555 111 2233",
                saveMode = "Continue",
                email = "attacker@example.com",
                role = "Admin",
                isActive = false,
                permissions = "admin"
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        user.Email.Should().Be("immutable@example.com");
        user.Role.Should().Be(UserRole.Rider);
        user.IsActive.Should().BeTrue();
        user.FullName.Should().Be("Changed Name");
    }

    private static async Task<User> AuthenticateAsync(HttpClient client, string email, TestStores stores)
    {
        RegisterRequest register = new(email, "StrongPass1!", "StrongPass1!", "Safe Rider", "+52 555 555 5555", "Rider", true);
        await client.PostAsJsonAsync("/api/v1/auth/register", register);
        HttpResponseMessage loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(register.Email, register.Password));
        LoginEnvelope? login = await loginResponse.Content.ReadFromJsonAsync<LoginEnvelope>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Data.AccessToken);

        return stores.Users.Users.Single(user => user.Email == email);
    }

    private static object ValidContinuePayload(string fullName) => new
    {
        fullName,
        phoneNumber = "+52 555 555 5555",
        dateOfBirth = "1995-01-15",
        addressOrZone = "Centro",
        primaryCity = "Toluca",
        provisionalEmergencyContactName = "Contacto",
        provisionalEmergencyContactPhone = "+52 555 111 2233",
        saveMode = "Continue"
    };

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
                    ["Jwt:Key"] = new string('R', 48),
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
                services.AddSingleton<IDriverProfileRepository>(stores.DriverProfiles);
            });
        });
    }

    private sealed class TestStores
    {
        public InMemoryUserRepository Users { get; } = new();

        public InMemoryRefreshTokenRepository RefreshTokens { get; } = new();

        public InMemoryDriverProfileRepository DriverProfiles { get; } = new();
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

    private sealed class InMemoryDriverProfileRepository : IDriverProfileRepository
    {
        public List<DriverProfile> Profiles { get; } = [];

        public Task<DriverProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(Profiles.FirstOrDefault(profile => profile.UserId == userId));

        public Task AddAsync(DriverProfile profile, CancellationToken cancellationToken)
        {
            Profiles.Add(profile);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed record LoginEnvelope(bool Success, LoginData Data);

    private sealed record LoginData(string AccessToken, string RefreshToken);
}
