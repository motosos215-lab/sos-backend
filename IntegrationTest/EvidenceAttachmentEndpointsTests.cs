using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.EmergencyResolution.Application;
using MotoSOS.API.Modules.EmergencyResolution.Domain;
using MotoSOS.API.Modules.EvidenceAttachments.Application;
using MotoSOS.API.Modules.EvidenceAttachments.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class EvidenceAttachmentEndpointsTests
{
    [Fact]
    public async Task EvidenceEndpointsRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new Stores());
        HttpClient client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/rider/evidence-attachments", ValidBody())).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/rider/evidence-attachments")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/admin/evidence-attachments")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RiderCreatesIdempotentMetadataOnlyEvidenceAndSoftDeletes()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient rider = factory.CreateClient(); User user = await AuthenticateAsync(rider, "evidence-rider@example.com", UserRole.Rider, stores); Seed(stores, user.Id);

        HttpResponseMessage create = await rider.PostAsJsonAsync("/api/v1/rider/evidence-attachments", ValidBody());
        HttpResponseMessage duplicate = await rider.PostAsJsonAsync("/api/v1/rider/evidence-attachments", ValidBody(description: "changed"));
        string body = await create.Content.ReadAsStringAsync();
        string id = stores.Evidence.Items.Single().Id;

        create.StatusCode.Should().Be(HttpStatusCode.OK);
        duplicate.StatusCode.Should().Be(HttpStatusCode.OK);
        stores.Evidence.Items.Should().ContainSingle();
        body.Should().Contain("registeredByUserId").And.Contain("targetType").And.NotContain("fileContent").And.NotContain("base64").And.NotContain("imageBytes").And.NotContain("polyline");
        (await rider.GetAsync("/api/v1/rider/evidence-attachments")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await rider.GetAsync($"/api/v1/rider/evidence-attachments/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await rider.PostAsync($"/api/v1/rider/evidence-attachments/{id}/delete", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await rider.PostAsync($"/api/v1/rider/evidence-attachments/{id}/delete", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        stores.Evidence.Items.Single().Status.Should().Be(EvidenceAttachmentStatus.MarkedDeleted);
    }

    [Fact]
    public async Task RiderCannotCreateForForeignIncidentAndInvalidPayloadIsRejected()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient owner = factory.CreateClient(); HttpClient other = factory.CreateClient(); User ownerUser = await AuthenticateAsync(owner, "evidence-owner@example.com", UserRole.Rider, stores); await AuthenticateAsync(other, "evidence-other@example.com", UserRole.Rider, stores); Seed(stores, ownerUser.Id);

        (await other.PostAsJsonAsync("/api/v1/rider/evidence-attachments", ValidBody())).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.PostAsJsonAsync("/api/v1/rider/evidence-attachments", new { incidentId = "incident", alertDispatchId = "alert", clientEvidenceId = "client", evidenceType = "Photo", source = "RiderMobileApp", fileName = "evidence.jpg", contentType = "image/jpeg", sizeBytes = 10, capturedAtUtc = DateTimeOffset.UtcNow })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PostAsJsonAsync("/api/v1/rider/evidence-attachments", new { incidentId = "incident", clientEvidenceId = "client", evidenceType = "Photo", source = "RiderMobileApp", fileName = "evidence.jpg", contentType = "image/jpeg", sizeBytes = 10, capturedAtUtc = DateTimeOffset.UtcNow, fileContent = "not accepted" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AssignedMonitorCanCreateAndUnassignedMonitorGetsNotFound()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient rider = factory.CreateClient(); HttpClient monitor = factory.CreateClient(); User riderUser = await AuthenticateAsync(rider, "evidence-owner2@example.com", UserRole.Rider, stores); User monitorUser = await AuthenticateAsync(monitor, "evidence-monitor@example.com", UserRole.Monitor, stores); Seed(stores, riderUser.Id, monitorUser.Id);

        (await monitor.PostAsJsonAsync("/api/v1/monitor/evidence-attachments", ValidBody(incidentId: null, alertDispatchId: "alert", source: "MonitorMobileApp"))).StatusCode.Should().Be(HttpStatusCode.OK);
        stores.Evidence.Items.Single().UserId.Should().Be(riderUser.Id);
        stores.Evidence.Items.Single().RegisteredByUserId.Should().Be(monitorUser.Id);
        stores.Contacts.Items.Clear();
        (await monitor.PostAsJsonAsync("/api/v1/monitor/evidence-attachments", ValidBody(incidentId: null, alertDispatchId: "alert", clientId: "other", source: "MonitorMobileApp"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminIsReadOnlyAndRiderMonitorCannotUseAdminList()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient rider = factory.CreateClient(); HttpClient admin = factory.CreateClient(); HttpClient monitor = factory.CreateClient(); User riderUser = await AuthenticateAsync(rider, "evidence-rider-admin@example.com", UserRole.Rider, stores); await AuthenticateAsync(admin, "evidence-admin@example.com", UserRole.Admin, stores); await AuthenticateAsync(monitor, "evidence-monitor-admin@example.com", UserRole.Monitor, stores); Seed(stores, riderUser.Id); await rider.PostAsJsonAsync("/api/v1/rider/evidence-attachments", ValidBody()); string id = stores.Evidence.Items.Single().Id;

        (await admin.GetAsync("/api/v1/admin/evidence-attachments")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync($"/api/v1/admin/evidence-attachments/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/v1/admin/evidence-attachments/missing")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.PostAsJsonAsync("/api/v1/rider/evidence-attachments", ValidBody())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await rider.GetAsync("/api/v1/admin/evidence-attachments")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await monitor.GetAsync("/api/v1/admin/evidence-attachments")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.GetAsync("/api/v1/admin/evidence-attachments?pageSize=101")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static object ValidBody(string? incidentId = "incident", string? alertDispatchId = null, string? clientId = "client", string source = "RiderMobileApp", string description = "safe") => new { incidentId, alertDispatchId, emergencyResolutionReportId = (string?)null, clientEvidenceId = clientId, evidenceType = "Photo", source, fileName = "evidence.jpg", contentType = "image/jpeg", sizeBytes = 10, sha256Hash = new string('a', 64), clientStorageReference = "local://evidence/evidence.jpg", storageProvider = "None", description, capturedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1), metadata = new Dictionary<string, string> { ["camera"] = "rear" } };
    private static void Seed(Stores stores, string userId, string? monitorUserId = null) { stores.Incidents.Items.Add(new Incident { Id = "incident", UserId = userId, TripId = "trip", Status = IncidentStatus.Open }); stores.Alerts.Items.Add(new AlertDispatchRequest { Id = "alert", UserId = userId, IncidentId = "incident", TripId = "trip", Status = AlertDispatchStatus.PendingDispatch }); stores.Reports.Items.Add(new EmergencyResolutionReport { Id = "report", UserId = userId, IncidentId = "incident", AlertDispatchId = "alert", TripId = "trip", Summary = "done" }); stores.Attempts.Items.Add(new NotificationDeliveryAttempt { Id = "attempt", UserId = userId, IncidentId = "incident", AlertDispatchId = "alert", TripId = "trip", EmergencyContactId = "contact" }); if (monitorUserId is not null) stores.Contacts.Items.Add(new EmergencyContact { Id = "contact", LinkedUserId = monitorUserId, IsActive = true, InvitationStatus = EmergencyContactInvitationStatus.Linked }); }
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, UserRole role, Stores stores) { var register = new RegisterRequest(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555", "Rider", true); await client.PostAsJsonAsync("/api/v1/auth/register", register); User user = stores.Users.Items.Single(u => u.Email == email); user.Role = role; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('E', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:" + "Connection" + "String"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<IIncidentRepository>(stores.Incidents); services.AddSingleton<IAlertDispatchRepository>(stores.Alerts); services.AddSingleton<IEmergencyResolutionRepository>(stores.Reports); services.AddSingleton<INotificationDeliveryAttemptRepository>(stores.Attempts); services.AddSingleton<IMonitorLinkedContactRepository>(stores.Contacts); services.AddSingleton<IEvidenceAttachmentRepository>(stores.Evidence); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Incidents Incidents { get; } = new(); public Alerts Alerts { get; } = new(); public Reports Reports { get; } = new(); public Attempts Attempts { get; } = new(); public Contacts Contacts { get; } = new(); public Evidence Evidence { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User u, CancellationToken ct) { Items.Add(u); return Task.CompletedTask; } public Task UpdateAsync(User u, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string h, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == h)); public Task AddAsync(RefreshToken t, CancellationToken ct) { Items.Add(t); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken t, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Incidents : IIncidentRepository { public List<Incident> Items { get; } = []; public Task<Incident?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id)); public Task<Incident?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<Incident?>(null); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident i, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string u, IncidentStatus? s, string? t, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>([]); public Task<long> CountByUserIdAsync(string u, IncidentStatus? s, string? t, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(Incident i, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Alerts : IAlertDispatchRepository { public List<AlertDispatchRequest> Items { get; } = []; public Task<AlertDispatchRequest?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<AlertDispatchRequest?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<AlertDispatchRequest?>(null); public Task<(AlertDispatchRequest AlertDispatch, bool IsDuplicate)> AddOrGetDuplicateAsync(AlertDispatchRequest a, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<AlertDispatchRequest>> ListByUserIdAsync(string u, AlertDispatchStatus? s, string? i, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<AlertDispatchRequest>>([]); public Task<long> CountByUserIdAsync(string u, AlertDispatchStatus? s, string? i, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(AlertDispatchRequest a, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Reports : IEmergencyResolutionRepository { public List<EmergencyResolutionReport> Items { get; } = []; public Task<EmergencyResolutionReport?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(r => r.Id == id)); public Task<EmergencyResolutionReport?> GetByIncidentIdAsync(string i, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(r => r.IncidentId == i)); public Task<EmergencyResolutionReport?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<EmergencyResolutionReport?>(null); public Task<(EmergencyResolutionReport Report, bool IsDuplicate)> AddOrGetDuplicateAsync(EmergencyResolutionReport r, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<EmergencyResolutionReport>> ListByUserIdAsync(string u, EmergencyResolutionOutcome? o, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyResolutionReport>>([]); public Task<long> CountByUserIdAsync(string u, EmergencyResolutionOutcome? o, CancellationToken ct) => Task.FromResult(0L); }
    private sealed class Attempts : INotificationDeliveryAttemptRepository { public List<NotificationDeliveryAttempt> Items { get; } = []; public Task<NotificationDeliveryAttempt?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id)); public Task<NotificationDeliveryAttempt?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult<NotificationDeliveryAttempt?>(null); public Task<(NotificationDeliveryAttempt Attempt, bool IsDuplicate)> AddOrGetDuplicateAsync(NotificationDeliveryAttempt a, CancellationToken ct) => throw new NotImplementedException(); public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Items.Where(n => n.UserId == u && (a is null || n.AlertDispatchId == a) && (i is null || n.IncidentId == i)).ToArray()); public Task<long> CountByUserIdAsync(string u, string? a, string? i, NotificationDeliveryStatus? s, CancellationToken ct) => Task.FromResult(0L); public Task UpdateAsync(NotificationDeliveryAttempt a, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Contacts : IMonitorLinkedContactRepository { public List<EmergencyContact> Items { get; } = []; public Task<IReadOnlyList<EmergencyContact>> GetActiveLinkedByLinkedUserIdAsync(string id, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyContact>>(Items.Where(c => c.LinkedUserId == id && c.IsActive && c.InvitationStatus == EmergencyContactInvitationStatus.Linked).ToArray()); }
    private sealed class Evidence : IEvidenceAttachmentRepository { public List<EvidenceAttachment> Items { get; } = []; public Task<EvidenceAttachment?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(e => e.Id == id)); public Task<EvidenceAttachment?> GetByIdempotencyKeyAsync(string k, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(e => e.IdempotencyKey == k)); public Task<(EvidenceAttachment EvidenceAttachment, bool IsDuplicate)> AddOrGetDuplicateAsync(EvidenceAttachment e, CancellationToken ct) { EvidenceAttachment? existing = Items.FirstOrDefault(x => x.IdempotencyKey == e.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Items.Add(e); return Task.FromResult((e, false)); } public Task UpdateAsync(EvidenceAttachment e, CancellationToken ct) => Task.CompletedTask; public Task<IReadOnlyList<EvidenceAttachment>> ListByUserIdAsync(string u, EvidenceAttachmentQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<EvidenceAttachment>>(Items.Where(e => e.UserId == u && (!q.Status.HasValue || e.Status == q.Status)).ToArray()); public Task<long> CountByUserIdAsync(string u, EvidenceAttachmentQuery q, CancellationToken ct) => Task.FromResult((long)Items.Count(e => e.UserId == u && (!q.Status.HasValue || e.Status == q.Status))); public Task<IReadOnlyList<EvidenceAttachment>> ListAsync(EvidenceAttachmentQuery q, CancellationToken ct) => Task.FromResult<IReadOnlyList<EvidenceAttachment>>(Items); public Task<long> CountAsync(EvidenceAttachmentQuery q, CancellationToken ct) => Task.FromResult((long)Items.Count); }
}
