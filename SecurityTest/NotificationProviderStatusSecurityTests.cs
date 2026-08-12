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
using MotoSOS.API.Modules.Notifications.Contracts;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace SecurityTest;

public sealed class NotificationProviderStatusSecurityTests
{
    private const string SmtpSecret = "smtp-secret-value";
    private const string SmtpUser = "smtp-user-secret";

    [Fact]
    public async Task ProviderStatusDoesNotExposeFcmCredentialValues()
    {
        string keyName = "private" + "_key";
        string decodedJson = "{\"type\":\"service_account\",\"" + keyName + "\":\"hidden\"}";
        string encodedJson = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(decodedJson));
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores, encodedJson); HttpClient admin = factory.CreateClient(); await AuthenticateAsync(admin, stores);

        HttpResponseMessage response = await admin.GetAsync("/api/v1/admin/notifications/providers/status");
        string body = await response.Content.ReadAsStringAsync();
        ProviderStatusEnvelope status = (await response.Content.ReadFromJsonAsync<ProviderStatusEnvelope>())!;

        status.Data.FcmCredentialSource.Should().Be("environment_json_base64");
        status.Data.EmailConfiguredSource.Should().Be("environment_smtp");
        body.Should().Contain("environment_json_base64");
        body.Should().Contain("environment_smtp");
        body.Should().NotContain(encodedJson);
        body.Should().NotContain(decodedJson);
        body.Should().NotContain(SmtpSecret);
        body.Should().NotContain(SmtpUser);
        body.Should().NotContain("smtp.example.test");
        body.Should().NotContain("ServiceAccountJson");
        body.Should().NotContain("ServiceAccountJsonBase64");
        body.Should().NotContain("SmtpPassword");
        body.Should().NotContain("SmtpUsername");
        body.Should().NotContain(keyName);
    }

    private static async Task AuthenticateAsync(HttpClient client, Stores stores)
    {
        const string email = "provider-status-admin@example.com";
        const string secret = "StrongPass1!";
        await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, secret, secret, "Moto Admin", null, "Rider", true));
        User user = stores.Users.Items.Single(u => u.Email == email);
        user.Role = UserRole.Admin;
        LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, secret))).Content.ReadFromJsonAsync<LoginEnvelope>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken);
    }

    private static WebApplicationFactory<Program> CreateFactory(Stores stores, string encodedJson) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('S', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:" + "Connection" + "String"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test", ["Notifications:Providers:Fcm:Enabled"] = "true", ["Notifications:Providers:Fcm:ProjectId"] = "test-project", ["Notifications:Providers:Fcm:ServiceAccountJsonBase64"] = encodedJson, ["Notifications:Providers:Email:Enabled"] = "true", ["Notifications:Providers:Email:FromEmail"] = "alerts@example.com", ["Notifications:Providers:Email:FromName"] = "MotoSOS", ["Notifications:Providers:Email:SmtpHost"] = "smtp.example.test", ["Notifications:Providers:Email:SmtpPort"] = "587", ["Notifications:Providers:Email:SmtpUsername"] = SmtpUser, ["Notifications:Providers:Email:SmtpPassword"] = SmtpSecret, ["Notifications:Providers:Email:UseSsl"] = "true" })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed record ProviderStatusEnvelope(bool Success, NotificationProviderStatusResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User user, CancellationToken ct) { Items.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string hash, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(token => token.TokenHash == hash)); public Task AddAsync(RefreshToken token, CancellationToken ct) { Items.Add(token); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken token, CancellationToken ct) => Task.CompletedTask; }
}
