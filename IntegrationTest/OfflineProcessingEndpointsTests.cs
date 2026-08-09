using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Contracts;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Contracts;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Contracts;
using MotoSOS.API.Modules.LocationSharing.Application;
using MotoSOS.API.Modules.LocationSharing.Contracts;
using MotoSOS.API.Modules.OfflineIngestion.Application;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.OfflineProcessing.Contracts;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace IntegrationTest;

public sealed class OfflineProcessingEndpointsTests
{
    [Fact]
    public async Task OfflineProcessingEndpointsRequireAuthentication()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory(new Stores()); HttpClient client = factory.CreateClient();
        (await client.PostAsJsonAsync("/api/v1/offline-processing/run", new RunOfflineProcessingRequest(1))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/v1/offline-processing/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("Monitor")]
    [InlineData("Admin")]
    public async Task NonRidersAreForbidden(string role)
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient client = factory.CreateClient(); await AuthenticateAsync(client, $"offline-processing-{role.ToLowerInvariant()}@example.com", role, stores);
        (await client.GetAsync("/api/v1/offline-processing/status")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RiderProcessesOwnRecordsAndDoesNotExposePayload()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient riderClient = factory.CreateClient(); HttpClient otherClient = factory.CreateClient(); User rider = await AuthenticateAsync(riderClient, "offline-processing-rider@example.com", "Rider", stores); User other = await AuthenticateAsync(otherClient, "offline-processing-other@example.com", "Rider", stores);
        stores.Records.Items.Add(Record(rider.Id, OfflineIngestionItemType.LocalIncident, "{\"source\":\"MobileDetection\",\"cause\":\"CountdownTimeout\",\"riskLevel\":\"High\",\"occurredAtUtc\":\"2026-08-06T14:00:00Z\"}"));
        stores.Records.Items.Add(Record(rider.Id, OfflineIngestionItemType.MinorEvent, "{\"secret\":\"do-not-return\"}"));
        stores.Records.Items.Add(Record(other.Id, OfflineIngestionItemType.MinorEvent, "{\"secret\":\"other\"}"));

        HttpResponseMessage response = await riderClient.PostAsJsonAsync("/api/v1/offline-processing/run", new RunOfflineProcessingRequest(20));
        string body = await response.Content.ReadAsStringAsync();
        string statusBody = await (await riderClient.GetAsync("/api/v1/offline-processing/status")).Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("Processed").And.Contain("Skipped").And.NotContain("do-not-return");
        body.ToLowerInvariant().Should().NotContain("payload");
        statusBody.Should().Contain("processed").And.Contain("skipped");
        stores.Records.Items.Single(r => r.UserId == other.Id).ProcessingStatus.Should().Be(OfflineIngestionProcessingStatus.PendingProcessing);
    }

    [Fact]
    public async Task InvalidMaxItemsReturnsValidationError()
    {
        var stores = new Stores(); await using WebApplicationFactory<Program> factory = CreateFactory(stores); HttpClient client = factory.CreateClient(); await AuthenticateAsync(client, "offline-processing-validation@example.com", "Rider", stores);
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/offline-processing/run", new RunOfflineProcessingRequest(101));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest); (await response.Content.ReadAsStringAsync()).Should().Contain("validation_error");
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 6, 14, 0, 0, TimeSpan.Zero);
    private static OfflineIngestionRecord Record(string userId, OfflineIngestionItemType type, string payload) => new() { UserId = userId, MobileDeviceId = "mobile", TripId = "trip", BatchId = "batch", ClientEventId = Guid.NewGuid().ToString(), Type = type, PayloadVersion = 1, SchemaVersion = 1, IdempotencyKey = Guid.NewGuid().ToString(), AckId = Guid.NewGuid().ToString(), Payload = payload, OccurredAtUtc = Now, ReceivedAtUtc = Now, CreatedAtUtc = Now, ProcessingStatus = OfflineIngestionProcessingStatus.PendingProcessing };
    private static async Task<User> AuthenticateAsync(HttpClient client, string email, string role, Stores stores) { var register = new RegisterRequest(email, "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555 555 5555", "Rider", true); await client.PostAsJsonAsync("/api/v1/auth/register", register); User user = stores.Users.Items.Single(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)); if (role == "Monitor") user.Role = UserRole.Monitor; if (role == "Admin") user.Role = UserRole.Admin; LoginEnvelope login = (await (await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, register.Password))).Content.ReadFromJsonAsync<LoginEnvelope>())!; client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Data.AccessToken); return user; }
    private static WebApplicationFactory<Program> CreateFactory(Stores stores) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Testing"); builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = "MotoSOS", ["Jwt:Audience"] = "MotoSOS.Clients", ["Jwt:Key"] = new string('P', 48), ["Jwt:AccessTokenMinutes"] = "15", ["Jwt:RefreshTokenDays"] = "7", ["Jwt:RefreshTokenRememberMeDays"] = "30", ["MongoDb:ConnectionString"] = string.Empty, ["MongoDb:DatabaseName"] = "MotoSOS_Test" })); builder.ConfigureTestServices(services => { services.AddSingleton<IUserRepository>(stores.Users); services.AddSingleton<IRefreshTokenRepository>(stores.RefreshTokens); services.AddSingleton<IOfflineIngestionRepository>(stores.Records); services.AddSingleton<IIncidentService, Incidents>(); services.AddSingleton<IAlertDispatchService, Alerts>(); services.AddSingleton<ILocationSharingService, Locations>(); }); });
    private sealed record LoginEnvelope(bool Success, LoginResponse Data);
    private sealed class Stores { public Users Users { get; } = new(); public RefreshTokens RefreshTokens { get; } = new(); public Records Records { get; } = new(); }
    private sealed class Users : IUserRepository { public List<User> Items { get; } = []; public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id)); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email)); public Task AddAsync(User user, CancellationToken ct) { Items.Add(user); return Task.CompletedTask; } public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class RefreshTokens : IRefreshTokenRepository { public List<RefreshToken> Items { get; } = []; public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == tokenHash)); public Task AddAsync(RefreshToken token, CancellationToken ct) { Items.Add(token); return Task.CompletedTask; } public Task UpdateAsync(RefreshToken token, CancellationToken ct) => Task.CompletedTask; }
    private sealed class Records : IOfflineIngestionRepository { public List<OfflineIngestionRecord> Items { get; } = []; public Task<OfflineIngestionRecord?> GetByIdempotencyKeyAsync(string key, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(r => r.IdempotencyKey == key)); public Task<(OfflineIngestionRecord Record, bool IsDuplicate)> AddOrGetDuplicateAsync(OfflineIngestionRecord record, CancellationToken ct) { Items.Add(record); return Task.FromResult((record, false)); } public Task<IReadOnlyList<OfflineIngestionRecord>> ListPendingByUserIdAsync(string userId, int maxItems, CancellationToken ct) => Task.FromResult<IReadOnlyList<OfflineIngestionRecord>>(Items.Where(r => r.UserId == userId && r.ProcessingStatus == OfflineIngestionProcessingStatus.PendingProcessing).Take(maxItems).ToArray()); public Task<OfflineIngestionRecord?> TryMarkProcessingAsync(string id, string userId, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord? record = Items.FirstOrDefault(r => r.Id == id && r.UserId == userId && r.ProcessingStatus == OfflineIngestionProcessingStatus.PendingProcessing); if (record is null) return Task.FromResult<OfflineIngestionRecord?>(null); record.ProcessingStatus = OfflineIngestionProcessingStatus.Processing; return Task.FromResult<OfflineIngestionRecord?>(record); } public Task MarkProcessedAsync(string id, string userId, string remoteRecordId, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord record = Items.Single(r => r.Id == id); record.ProcessingStatus = OfflineIngestionProcessingStatus.Processed; record.RemoteRecordId = remoteRecordId; return Task.CompletedTask; } public Task MarkIgnoredAsync(string id, string userId, string reason, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord record = Items.Single(r => r.Id == id); record.ProcessingStatus = OfflineIngestionProcessingStatus.Ignored; record.ProcessingReason = reason; return Task.CompletedTask; } public Task MarkFailedPermanentAsync(string id, string userId, string errorCode, string errorMessage, DateTimeOffset now, CancellationToken ct) { OfflineIngestionRecord record = Items.Single(r => r.Id == id); record.ProcessingStatus = OfflineIngestionProcessingStatus.FailedPermanent; record.ProcessingErrorCode = errorCode; return Task.CompletedTask; } public Task<long> CountByUserIdAndStatusAsync(string userId, OfflineIngestionProcessingStatus status, CancellationToken ct) => Task.FromResult((long)Items.Count(r => r.UserId == userId && r.ProcessingStatus == status)); }
    private sealed class Incidents : IIncidentService { public Task<CreateIncidentResponse> CreateAsync(string userId, CreateIncidentRequest request, CancellationToken ct) => Task.FromResult(new CreateIncidentResponse(new IncidentResponse("incident-1", request.TripId!, "vehicle", "mobile", null, request.Source!, request.Cause!, request.RiskLevel!, "Open", null, null, request.OccurredAtUtc!.Value, Now, Now, null, null, null, null))); public Task<GetIncidentsResponse> ListAsync(string userId, string? status, string? tripId, int? pageNumber, int? pageSize, CancellationToken ct) => throw new NotImplementedException(); public Task<GetIncidentResponse> GetAsync(string userId, string incidentId, CancellationToken ct) => throw new NotImplementedException(); public Task<CancelFalsePositiveResponse> CancelFalsePositiveAsync(string userId, string incidentId, CancelFalsePositiveRequest request, CancellationToken ct) => throw new NotImplementedException(); public Task<CloseIncidentResponse> CloseAsync(string userId, string incidentId, CloseIncidentRequest request, CancellationToken ct) => throw new NotImplementedException(); }
    private sealed class Alerts : IAlertDispatchService { public Task<CreateAlertDispatchResponse> CreateAsync(string userId, CreateAlertDispatchRequest request, CancellationToken ct) => Task.FromResult(new CreateAlertDispatchResponse(new AlertDispatchResponse("alert-1", request.IncidentId!, "trip", "vehicle", "mobile", null, request.Priority!, request.Reason!, "PendingDispatch", request.RequestedAtUtc!.Value, Now, Now, null, null, null, 1))); public Task<GetAlertDispatchesResponse> ListAsync(string userId, string? status, string? incidentId, int? pageNumber, int? pageSize, CancellationToken ct) => throw new NotImplementedException(); public Task<GetAlertDispatchResponse> GetAsync(string userId, string id, CancellationToken ct) => throw new NotImplementedException(); public Task<CancelAlertDispatchResponse> CancelAsync(string userId, string id, CancelAlertDispatchRequest request, CancellationToken ct) => throw new NotImplementedException(); }
    private sealed class Locations : ILocationSharingService { public Task<ShareLocationSnapshotResponse> ShareAsync(string userId, ShareLocationSnapshotRequest request, CancellationToken ct) => Task.FromResult(new ShareLocationSnapshotResponse(new LocationSnapshotResponse(request.IncidentId!, "trip", request.Latitude!.Value, request.Longitude!.Value, request.AccuracyMeters, request.Source!, request.RecordedAtUtc!.Value, Now, true, false))); public Task<GetLocationSnapshotResponse> GetForMonitorAsync(string monitorUserId, string notificationDeliveryAttemptId, CancellationToken ct) => throw new NotImplementedException(); public Task<GetLocationSnapshotResponse> GetForRiderAsync(string riderUserId, string incidentId, CancellationToken ct) => throw new NotImplementedException(); }
}
