using FluentAssertions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.EmergencyResolution.Domain;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.OperationalDashboard.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.OperationalDashboard;

public sealed class OperationalDashboardServiceTests
{
    [Fact]
    public async Task AdminCanGetSummaryAndRolesAreEnforced()
    {
        User admin = User(UserRole.Admin); var repo = Repository(); var service = new OperationalDashboardService(new Users(admin), repo);
        (await service.GetSummaryAsync(admin.Id, CancellationToken.None)).Users.Total.Should().Be(4);
        await Assert.ThrowsAsync<ForbiddenAppException>(() => new OperationalDashboardService(new Users(User(UserRole.Rider)), repo).GetSummaryAsync("rider", CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenAppException>(() => new OperationalDashboardService(new Users(User(UserRole.Monitor)), repo).GetSummaryAsync("monitor", CancellationToken.None));
    }

    [Fact]
    public async Task SummaryCalculatesAllSections()
    {
        OperationalDashboardService service = Service();

        var summary = await service.GetSummaryAsync("admin", CancellationToken.None);

        summary.Users.Riders.Should().Be(2);
        summary.Onboarding.Completed.Should().Be(1);
        summary.Onboarding.InProgress.Should().Be(1);
        summary.Trips.Active.Should().Be(1);
        summary.Incidents.FalsePositiveCancelled.Should().Be(1);
        summary.Alerts.PendingDispatch.Should().Be(1);
        summary.Notifications.SimulatedSent.Should().Be(1);
        summary.Acknowledgements.Acknowledged.Should().Be(1);
        summary.ResolutionReports.Total.Should().Be(3);
        summary.OfflineProcessing.FailedPermanent.Should().Be(1);
    }

    [Fact]
    public async Task IncidentListFiltersByDateStatusAndPaginates()
    {
        OperationalDashboardService service = Service();
        var query = new OperationalDashboardQuery(Now.AddDays(-1), Now.AddDays(1), "Closed", 1, 1) { ParsedStatus = IncidentStatus.Closed };

        var response = await service.ListIncidentsAsync("admin", query, CancellationToken.None);

        response.Items.Should().ContainSingle();
        response.Items[0].Status.Should().Be("Closed");
        response.Total.Should().Be(1);
    }

    [Fact]
    public async Task ResponseTimesIgnoreNullsAndAvoidDivideByZero()
    {
        var response = await Service().GetResponseTimesAsync("admin", new OperationalDashboardQuery(null, null, null, null, null), CancellationToken.None);
        response.TotalReports.Should().Be(3);
        response.ReportsWithResponseTime.Should().Be(2);
        response.AverageResponseTimeSeconds.Should().Be(90);
        response.MinResponseTimeSeconds.Should().Be(60);
        response.MaxResponseTimeSeconds.Should().Be(120);

        var empty = await new OperationalDashboardService(new Users(User(UserRole.Admin)), new DashboardRepository { Reports = [Report(null)] }).GetResponseTimesAsync("admin", new OperationalDashboardQuery(null, null, null, null, null), CancellationToken.None);
        empty.AverageResponseTimeSeconds.Should().BeNull();
        empty.MinResponseTimeSeconds.Should().BeNull();
        empty.MaxResponseTimeSeconds.Should().BeNull();
    }

    [Fact]
    public async Task ResolutionOutcomesGroupByOutcomeAndOfflineDoesNotExposePayload()
    {
        OperationalDashboardService service = Service();
        (await service.GetResolutionOutcomesAsync("admin", new OperationalDashboardQuery(null, null, null, null, null), CancellationToken.None)).Items.Should().Contain(i => i.Outcome == "FalsePositive" && i.Total == 2);
        (await service.GetOfflineProcessingAsync("admin", CancellationToken.None)).Processed.Should().Be(1);
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero);
    private static OperationalDashboardService Service() => new(new Users(User(UserRole.Admin)), Repository());
    private static User User(UserRole role) => new() { Id = role.ToString().ToLowerInvariant(), Role = role, IsActive = true, Email = $"{Guid.NewGuid()}@example.com" };
    private static DashboardRepository Repository() => new()
    {
        Users = [User(UserRole.Admin), User(UserRole.Rider), User(UserRole.Rider), User(UserRole.Monitor)],
        OperationalOnboarding = 1,
        Trips = [Trip(TripStatus.Active), Trip(TripStatus.Finished)],
        Incidents = [Incident(IncidentStatus.Open), Incident(IncidentStatus.Closed), Incident(IncidentStatus.FalsePositiveCancelled)],
        Alerts = [AlertDispatchStatus.PendingDispatch, AlertDispatchStatus.Completed, AlertDispatchStatus.Cancelled],
        Notifications = [NotificationDeliveryStatus.Prepared, NotificationDeliveryStatus.SimulatedSent, NotificationDeliveryStatus.Failed, NotificationDeliveryStatus.Cancelled],
        Acks = [AlertAcknowledgementStatus.Pending, AlertAcknowledgementStatus.Viewed, AlertAcknowledgementStatus.Acknowledged, AlertAcknowledgementStatus.Declined],
        Reports = [Report(60), Report(120), Report(null)],
        Offline = [OfflineIngestionProcessingStatus.PendingProcessing, OfflineIngestionProcessingStatus.Processing, OfflineIngestionProcessingStatus.Processed, OfflineIngestionProcessingStatus.Ignored, OfflineIngestionProcessingStatus.FailedPermanent]
    };
    private static Trip Trip(TripStatus status) => new() { Status = status, CreatedAtUtc = Now };
    private static Incident Incident(IncidentStatus status) => new() { Id = status.ToString(), TripId = "trip", Status = status, Source = IncidentSource.MobileDetection, Cause = IncidentCause.CountdownTimeout, RiskLevel = IncidentRiskLevel.High, OccurredAtUtc = Now, CreatedAtUtc = Now, ClosedAtUtc = status == IncidentStatus.Closed ? Now.AddMinutes(10) : null, CancelledAtUtc = status == IncidentStatus.FalsePositiveCancelled ? Now.AddMinutes(2) : null };
    private static EmergencyResolutionReport Report(long? seconds) => new() { Outcome = seconds == 120 ? EmergencyResolutionOutcome.UserSafe : EmergencyResolutionOutcome.FalsePositive, ResponseTimeSeconds = seconds, CreatedAtUtc = Now };
    private sealed class Users(User user) : IUserRepository { public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<User?>(user.Id == id ? user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken ct) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask; }
    private sealed class DashboardRepository : IOperationalDashboardRepository
    {
        public List<User> Users { get; init; } = []; public long OperationalOnboarding { get; init; }
        public List<Trip> Trips { get; init; } = []; public List<Incident> Incidents { get; init; } = []; public List<AlertDispatchStatus> Alerts { get; init; } = []; public List<NotificationDeliveryStatus> Notifications { get; init; } = []; public List<AlertAcknowledgementStatus> Acks { get; init; } = []; public List<EmergencyResolutionReport> Reports { get; init; } = []; public List<OfflineIngestionProcessingStatus> Offline { get; init; } = [];
        public Task<long> CountUsersAsync(UserRole? r, CancellationToken ct) => Task.FromResult((long)Users.Count(u => !r.HasValue || u.Role == r)); public Task<long> CountOperationalOnboardingAsync(CancellationToken ct) => Task.FromResult(OperationalOnboarding); public Task<long> CountTripsAsync(TripStatus? s, CancellationToken ct) => Task.FromResult((long)Trips.Count(t => !s.HasValue || t.Status == s)); public Task<long> CountIncidentsAsync(IncidentStatus? s, DateTimeOffset? f, DateTimeOffset? t, CancellationToken ct) => Task.FromResult((long)FilterIncidents(s, f, t).Count()); public Task<IReadOnlyList<Incident>> ListIncidentsAsync(IncidentStatus? s, DateTimeOffset? f, DateTimeOffset? t, int p, int z, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>(FilterIncidents(s, f, t).OrderByDescending(i => i.CreatedAtUtc).Skip((p - 1) * z).Take(z).ToArray()); public Task<long> CountAlertDispatchesAsync(AlertDispatchStatus? s, CancellationToken ct) => Task.FromResult((long)Alerts.Count(a => !s.HasValue || a == s)); public Task<long> CountNotificationsAsync(NotificationDeliveryStatus? s, CancellationToken ct) => Task.FromResult((long)Notifications.Count(n => !s.HasValue || n == s)); public Task<long> CountAcknowledgementsAsync(AlertAcknowledgementStatus? s, CancellationToken ct) => Task.FromResult((long)Acks.Count(a => !s.HasValue || a == s)); public Task<long> CountResolutionReportsAsync(DateTimeOffset? f, DateTimeOffset? t, CancellationToken ct) => Task.FromResult((long)FilterReports(f, t).Count()); public Task<IReadOnlyList<EmergencyResolutionReport>> ListResolutionReportsAsync(DateTimeOffset? f, DateTimeOffset? t, CancellationToken ct) => Task.FromResult<IReadOnlyList<EmergencyResolutionReport>>(FilterReports(f, t).ToArray()); public Task<long> CountOfflineRecordsAsync(OfflineIngestionProcessingStatus s, CancellationToken ct) => Task.FromResult((long)Offline.Count(o => o == s)); private IEnumerable<Incident> FilterIncidents(IncidentStatus? s, DateTimeOffset? f, DateTimeOffset? t) => Incidents.Where(i => (!s.HasValue || i.Status == s) && (!f.HasValue || i.CreatedAtUtc >= f) && (!t.HasValue || i.CreatedAtUtc <= t)); private IEnumerable<EmergencyResolutionReport> FilterReports(DateTimeOffset? f, DateTimeOffset? t) => Reports.Where(r => (!f.HasValue || r.CreatedAtUtc >= f) && (!t.HasValue || r.CreatedAtUtc <= t));
    }
}
