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
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Contracts;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace SecurityTest;

public sealed class EmergencyContactSecurityTests
{
    [Fact]
    public async Task EmergencyContactsDoNotExposePasswordHashOrRefreshToken()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "contact-safe@example.com", stores);
        await client.PostAsJsonAsync("/api/v1/emergency-contacts", ValidCreateRequest());

        HttpResponseMessage response = await client.GetAsync("/api/v1/emergency-contacts");
        string content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().NotContain("PasswordHash");
        content.Should().NotContain("passwordHash");
        content.Should().NotContain("refreshToken");
    }

    [Fact]
    public async Task UnexpectedFieldsDoNotChangeUserSecurityFields()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        User user = await AuthenticateAsync(client, "contact-immutable@example.com", stores);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/emergency-contacts", new
        {
            fullName = "Maria Lopez",
            relationship = "Esposa",
            phoneNumber = "+52 5512345678",
            email = "maria@example.com",
            priority = 1,
            permissions = new { canViewRealTimeLocation = true, canReceiveCriticalAlerts = true, canViewIncidentHistory = false, canViewVitalSigns = false },
            saveMode = "Continue",
            role = "Admin",
            isActive = false,
            userEmail = "attacker@example.com"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        user.Email.Should().Be("contact-immutable@example.com");
        user.Role.Should().Be(UserRole.Rider);
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CannotAccessOtherUsersContactAndDeleteDoesNotPhysicallyRemove()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient firstClient = factory.CreateClient();
        HttpClient secondClient = factory.CreateClient();
        await AuthenticateAsync(firstClient, "contact-owner-security@example.com", stores);
        ContactEnvelope created = await CreateContactAsync(firstClient);
        await AuthenticateAsync(secondClient, "contact-other-security@example.com", stores);

        HttpResponseMessage otherAccess = await secondClient.GetAsync($"/api/v1/emergency-contacts/{created.Data.Contact.Id}");
        HttpResponseMessage delete = await firstClient.DeleteAsync($"/api/v1/emergency-contacts/{created.Data.Contact.Id}");

        otherAccess.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        stores.Contacts.Contacts.Should().ContainSingle(contact => contact.Id == created.Data.Contact.Id && !contact.IsActive && contact.InvitationStatus == EmergencyContactInvitationStatus.Revoked);
    }

    [Fact]
    public async Task LinkingCodeHasNoSensitiveDataAndRegenerates()
    {
        var stores = new TestStores();
        await using WebApplicationFactory<Program> factory = CreateFactory(stores);
        HttpClient client = factory.CreateClient();
        await AuthenticateAsync(client, "contact-code@example.com", stores);
        ContactEnvelope created = await CreateContactAsync(client);

        InviteEnvelope first = (await (await client.PostAsync($"/api/v1/emergency-contacts/{created.Data.Contact.Id}/invite", null)).Content.ReadFromJsonAsync<InviteEnvelope>())!;
        InviteEnvelope second = (await (await client.PostAsync($"/api/v1/emergency-contacts/{created.Data.Contact.Id}/invite", null)).Content.ReadFromJsonAsync<InviteEnvelope>())!;

        first.Data.Contact.LinkingCode.Should().NotContain("contact-code@example.com");
        first.Data.Contact.LinkingCode.Should().NotContain("Maria");
        second.Data.Contact.LinkingCode.Should().NotBe(first.Data.Contact.LinkingCode);
    }

    private static async Task<ContactEnvelope> CreateContactAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/emergency-contacts", ValidCreateRequest());
        return (await response.Content.ReadFromJsonAsync<ContactEnvelope>())!;
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

    private static CreateEmergencyContactRequest ValidCreateRequest() => new("Maria Lopez", "Esposa", "+52 5512345678", "maria@example.com", 1, new EmergencyContactPermissionsRequest(true, true, false, false), "Continue");
    private static WebApplicationFactory<Program> CreateFactory(TestStores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "MotoSOS",
            ["Jwt:Audience"] = "MotoSOS.Clients",
            ["Jwt:Key"] = new string('C', 48),
            ["Jwt:AccessTokenMinutes"] = "15",
            ["Jwt:RefreshTokenDays"] = "7",
            ["Jwt:RefreshTokenRememberMeDays"] = "30",
            ["MongoDb:ConnectionString"] = string.Empty,
            ["MongoDb:DatabaseName"] = "MotoSOS_Test"
        }));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<IDriverProfileRepository>(stores.DriverProfiles); services.AddSingleton<IDriverVehicleRepository>(stores.DriverVehicles); services.AddSingleton<IEmergencyContactRepository>(stores.Contacts);
        });
    });
    private sealed class TestStores { public InMemoryUserRepository Users { get; } = new(); public InMemoryRefreshTokenRepository RefreshTokens { get; } = new(); public InMemoryDriverProfileRepository DriverProfiles { get; } = new(); public InMemoryDriverVehicleRepository DriverVehicles { get; } = new(); public InMemoryEmergencyContactRepository Contacts { get; } = new(); }
    private sealed class InMemoryUserRepository : IUserRepository { public List<User> Users { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(user => user.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult(Users.FirstOrDefault(user => string.Equals(user.Email, email.Trim(), StringComparison.OrdinalIgnoreCase))); public Task AddAsync(User user, CancellationToken cancellationToken) { Users.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository { public List<RefreshToken> Tokens { get; } = []; public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) => Task.FromResult(Tokens.FirstOrDefault(token => token.TokenHash == tokenHash)); public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken) { Tokens.Add(refreshToken); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryDriverProfileRepository : IDriverProfileRepository { public Task<DriverProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<DriverProfile?>(null); public Task AddAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(DriverProfile profile, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryDriverVehicleRepository : IDriverVehicleRepository { public Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DriverVehicle>>([]); public Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<DriverVehicle?>(null); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(0); public Task AddAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(DriverVehicle vehicle, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryEmergencyContactRepository : IEmergencyContactRepository { public List<EmergencyContact> Contacts { get; } = []; public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EmergencyContact>>(Contacts.Where(contact => contact.UserId == userId && contact.IsActive).ToArray()); public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Contacts.FirstOrDefault(contact => contact.Id == id)); public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken cancellationToken) => Task.FromResult(Contacts.FirstOrDefault(contact => contact.LinkingCode == linkingCode)); public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Contacts.Count(contact => contact.UserId == userId && contact.IsActive)); public Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken) { Contacts.Add(contact); return Task.CompletedTask; } public Task UpdateAsync(EmergencyContact contact, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed record LoginEnvelope(bool Success, LoginData Data); private sealed record LoginData(string AccessToken, string RefreshToken); private sealed record ContactEnvelope(bool Success, CreateEmergencyContactResponse Data); private sealed record InviteEnvelope(bool Success, InviteEmergencyContactResponse Data);
}
