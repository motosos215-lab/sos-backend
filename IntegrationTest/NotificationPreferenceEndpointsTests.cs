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
using MotoSOS.API.Modules.NotificationPreferences.Application;
using MotoSOS.API.Modules.NotificationPreferences.Contracts;
using MotoSOS.API.Modules.NotificationPreferences.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class NotificationPreferenceEndpointsTests
{
    [Fact]
    public async Task EndpointsRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new Stores());
        HttpClient client = factory.CreateClient();

        (await client.GetAsync("/api/v1/notification-preferences/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PutAsJsonAsync("/api/v1/notification-preferences/me", Request())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCreatesDefaultsAndPutUpdatesOnlyMine()
    {
        var stores = new Stores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient rider = factory.CreateClient(); HttpClient other = factory.CreateClient();
        User riderUser = await AuthenticateAsync(rider, "preferences-rider@example.com", UserRole.Rider, stores);
        User otherUser = await AuthenticateAsync(other, "preferences-other@example.com", UserRole.Monitor, stores);

        PreferenceEnvelope defaults = (await (await rider.GetAsync("/api/v1/notification-preferences/me")).Content.ReadFromJsonAsync<PreferenceEnvelope>())!;
        UpdateEnvelope updated = (await (await rider.PutAsJsonAsync("/api/v1/notification-preferences/me", Request(push: false, email: true, quiet: true))).Content.ReadFromJsonAsync<UpdateEnvelope>())!;

        defaults.Data.Preferences.PushEnabled.Should().BeTrue();
        defaults.Data.Preferences.EmailEnabled.Should().BeFalse();
        defaults.Data.Preferences.TimeZone.Should().Be("America/Mexico_City");
        updated.Data.Preferences.PushEnabled.Should().BeFalse();
        updated.Data.Preferences.EmailEnabled.Should().BeTrue();
        updated.Data.Preferences.QuietHoursStartLocal.Should().Be("22:00");
        stores.Preferences.Items.Should().ContainSingle(p => p.UserId == riderUser.Id && !p.PushEnabled && p.EmailEnabled);
        stores.Preferences.Items.Should().NotContain(p => p.UserId == otherUser.Id);
    }

    [Fact]
    public async Task ValidationRejectsForbiddenUserIdAndInvalidQuietHours()
    {
        var stores = new Stores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient rider = factory.CreateClient(); await AuthenticateAsync(rider, "preferences-validation@example.com", UserRole.Rider, stores);

        HttpResponseMessage withUserId = await rider.PutAsJsonAsync("/api/v1/notification-preferences/me", new { userId = "other", pushEnabled = true, emailEnabled = false, smsEnabled = false, criticalAlertsEnabled = true, tripUpdatesEnabled = true, securityAlertsEnabled = true, marketingEnabled = false, quietHoursEnabled = false, quietHoursStartLocal = (string?)null, quietHoursEndLocal = (string?)null, timeZone = "America/Mexico_City" });
        HttpResponseMessage invalidQuietHours = await rider.PutAsJsonAsync("/api/v1/notification-preferences/me", Request(quiet: true, start: "22:00", end: "22:00"));
        string body = await (await rider.GetAsync("/api/v1/notification-preferences/me")).Content.ReadAsStringAsync();

        withUserId.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invalidQuietHours.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.ToLowerInvariant().Should().NotContain("userid").And.NotContain("token").And.NotContain("password");
    }

    private static UpdateNotificationPreferenceRequest Request(bool push = true, bool email = false, bool quiet = false, string? start = "22:00", string? end = "06:00") => new(push, email, false, true, true, true, false, quiet, quiet ? start : null, quiet ? end : null, "America/Mexico_City");
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, UserRole role, Stores stores) { const string secret = "StrongPass1!"; await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, secret, secret, "Moto User", null, role == UserRole.Monitor ? "Monitor" : "Rider", true)); User user = stores.Users.Items.Single(u => u.Email == email); user.Role = role; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, secret))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('P', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:" + "Connection" + "String"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<INotificationPreferenceRepository>(stores.Preferences); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed record PreferenceEnvelope(bool Success, GetNotificationPreferenceResponse Data);
    private sealed record UpdateEnvelope(bool Success, UpdateNotificationPreferenceResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Preferences Preferences { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase))); public Task AddAsync(User user, CancellationToken ct) { Items.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string hash, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == hash)); public Task AddAsync(RefreshToken token, CancellationToken ct) { Items.Add(token); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken token, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Preferences : INotificationPreferenceRepository { public List<NotificationPreference> Items { get; } = []; public Task<NotificationPreference?> GetByUserIdAsync(string userId, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(p => p.UserId == userId)); public Task AddAsync(NotificationPreference preference, CancellationToken ct) { Items.Add(preference); return Task.CompletedTask; } public Task UpdateAsync(NotificationPreference preference, CancellationToken ct) => Task.CompletedTask; }
}
