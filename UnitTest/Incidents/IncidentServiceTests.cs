using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Contracts;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace UnitTest.Incidents;

public sealed class IncidentServiceTests
{
    [Fact]
    public async Task RiderCanCreateIncidentWithReadyOnboardingAndOwnTrip()
    {
        User user = User(UserRole.Rider);
        Trip trip = Trip(user.Id, TripStatus.Active);
        var incidents = new InMemoryIncidentRepository();
        IncidentService service = CreateService(user, trip, incidents);

        CreateIncidentResponse response = await service.CreateAsync(user.Id, Request(trip.Id), CancellationToken.None);

        response.Incident.Status.Should().Be("Open");
        response.Incident.VehicleId.Should().Be(trip.VehicleId);
        incidents.Incidents.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateRequiresCompletedOnboardingAndOwnTrip()
    {
        User user = User(UserRole.Rider);
        User other = User(UserRole.Rider);
        Trip otherTrip = Trip(other.Id, TripStatus.Active);

        await Assert.ThrowsAsync<OnboardingNotReadyAppException>(() => CreateService(user, otherTrip, false).CreateAsync(user.Id, Request(otherTrip.Id), CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundAppException>(() => CreateService(user, otherTrip).CreateAsync(user.Id, Request(otherTrip.Id), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAcceptsFinishedTripAndIsIdempotent()
    {
        User user = User(UserRole.Rider);
        Trip trip = Trip(user.Id, TripStatus.Finished);
        var incidents = new InMemoryIncidentRepository();
        IncidentService service = CreateService(user, trip, incidents);
        string clientIncidentId = Guid.NewGuid().ToString();

        CreateIncidentResponse first = await service.CreateAsync(user.Id, Request(trip.Id, clientIncidentId), CancellationToken.None);
        CreateIncidentResponse duplicate = await service.CreateAsync(user.Id, Request(trip.Id, clientIncidentId), CancellationToken.None);

        duplicate.Incident.Id.Should().Be(first.Incident.Id);
        incidents.Incidents.Should().ContainSingle();
    }

    [Fact]
    public async Task ListGetCancelAndCloseRespectOwnershipAndIdempotency()
    {
        User user = User(UserRole.Rider);
        User other = User(UserRole.Rider);
        Incident own = Incident(user.Id, IncidentStatus.Open);
        Incident otherIncident = Incident(other.Id, IncidentStatus.Open);
        IncidentService service = CreateService(user, new InMemoryIncidentRepository(own, otherIncident));

        (await service.ListAsync(user.Id, null, null, null, null, CancellationToken.None)).Incidents.Should().ContainSingle(i => i.Id == own.Id);
        await Assert.ThrowsAsync<NotFoundAppException>(() => service.GetAsync(user.Id, otherIncident.Id, CancellationToken.None));

        CancelFalsePositiveResponse cancelled = await service.CancelFalsePositiveAsync(user.Id, own.Id, new CancelFalsePositiveRequest("ok", Now), CancellationToken.None);
        CancelFalsePositiveResponse secondCancel = await service.CancelFalsePositiveAsync(user.Id, own.Id, new CancelFalsePositiveRequest("ok", Now), CancellationToken.None);
        CloseIncidentResponse closed = await service.CloseAsync(user.Id, own.Id, new CloseIncidentRequest("Resolved", "done", Now), CancellationToken.None);
        CloseIncidentResponse secondClose = await service.CloseAsync(user.Id, own.Id, new CloseIncidentRequest("Resolved", "done", Now), CancellationToken.None);

        cancelled.Incident.Status.Should().Be("FalsePositiveCancelled");
        secondCancel.Incident.Status.Should().Be("FalsePositiveCancelled");
        closed.Incident.Status.Should().Be("Closed");
        secondClose.Incident.Status.Should().Be("Closed");
    }

    [Fact]
    public async Task CancelClosedIncidentFailsAndNonRidersAreForbidden()
    {
        User rider = User(UserRole.Rider);
        Incident closed = Incident(rider.Id, IncidentStatus.Closed);
        await Assert.ThrowsAsync<IncidentAlreadyClosedAppException>(() => CreateService(rider, new InMemoryIncidentRepository(closed)).CancelFalsePositiveAsync(rider.Id, closed.Id, new CancelFalsePositiveRequest(null, null), CancellationToken.None));
        User monitor = User(UserRole.Monitor);
        await Assert.ThrowsAsync<ForbiddenAppException>(() => CreateService(monitor).ListAsync(monitor.Id, null, null, null, null, CancellationToken.None));
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 6, 7, 10, 0, TimeSpan.Zero);
    private static IncidentService CreateService(User user, params object[] deps) => new(new InMemoryUserRepository(user), new StubOnboarding(deps.OfType<bool>().FirstOrDefault(true)), deps.OfType<InMemoryTripRepository>().FirstOrDefault() ?? new InMemoryTripRepository(deps.OfType<Trip>().ToArray()), deps.OfType<InMemoryIncidentRepository>().FirstOrDefault() ?? new InMemoryIncidentRepository(deps.OfType<Incident>().ToArray()), new IncidentIdempotencyKeyFactory(), new TestClock());
    private static User User(UserRole role) => new() { Email = $"{Guid.NewGuid()}@example.com", FullName = "Rider", Role = role, IsActive = true };
    private static Trip Trip(string userId, TripStatus status) => new() { UserId = userId, VehicleId = "vehicle", MobileDeviceId = "mobile", SmartwatchDeviceId = "watch", Status = status, StartedAtUtc = Now, FinishedAtUtc = status == TripStatus.Finished ? Now.AddMinutes(5) : null, CreatedAtUtc = Now };
    private static Incident Incident(string userId, IncidentStatus status) => new() { UserId = userId, TripId = "trip", VehicleId = "vehicle", MobileDeviceId = "mobile", ClientIncidentId = Guid.NewGuid().ToString(), IdempotencyKey = Guid.NewGuid().ToString(), Source = IncidentSource.MobileDetection, Cause = IncidentCause.CountdownTimeout, RiskLevel = IncidentRiskLevel.High, Status = status, OccurredAtUtc = Now, CreatedAtUtc = Now };
    private static CreateIncidentRequest Request(string tripId, string? clientIncidentId = null) => new(tripId, clientIncidentId ?? Guid.NewGuid().ToString(), "MobileDetection", "CountdownTimeout", "High", 87, 0.91, "Good", "rules-v1", "validation-v1", Now, null, null);
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => Now; }
    private sealed class StubOnboarding : IOnboardingService { private readonly bool _ready; public StubOnboarding(bool ready) { _ready = ready; } public Task<OnboardingStatusResponse> GetStatusAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(_ready ? new OnboardingStatusResponse(7, 7, 100, "Completed", true, []) : new OnboardingStatusResponse(7, 6, 86, "Confirmation", false, [])); }
    private sealed class InMemoryUserRepository : IUserRepository { private readonly User _user; public InMemoryUserRepository(User user) { _user = user; } public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult<User?>(_user.Id == id ? _user : null); public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<User?>(null); public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryTripRepository : ITripRepository { private readonly List<Trip> _trips; public InMemoryTripRepository(params Trip[] trips) { _trips = trips.ToList(); } public Task<Trip?> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(_trips.FirstOrDefault(t => t.UserId == userId && t.Status == TripStatus.Active)); public Task<Trip?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(_trips.FirstOrDefault(t => t.Id == id)); public Task<IReadOnlyList<Trip>> ListByUserIdAsync(string userId, TripStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Trip>>([]); public Task<long> CountByUserIdAsync(string userId, TripStatus? status, CancellationToken cancellationToken) => Task.FromResult(0L); public Task AddAsync(Trip trip, CancellationToken cancellationToken) => Task.CompletedTask; public Task UpdateAsync(Trip trip, CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class InMemoryIncidentRepository : IIncidentRepository { public List<Incident> Incidents { get; } public InMemoryIncidentRepository(params Incident[] incidents) { Incidents = incidents.ToList(); } public Task<Incident?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Incidents.FirstOrDefault(i => i.Id == id)); public Task<Incident?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult(Incidents.FirstOrDefault(i => i.IdempotencyKey == idempotencyKey)); public Task<(Incident Incident, bool IsDuplicate)> AddOrGetDuplicateAsync(Incident incident, CancellationToken cancellationToken) { Incident? existing = Incidents.FirstOrDefault(i => i.IdempotencyKey == incident.IdempotencyKey); if (existing is not null) return Task.FromResult((existing, true)); Incidents.Add(incident); return Task.FromResult((incident, false)); } public Task<IReadOnlyList<Incident>> ListByUserIdAsync(string userId, IncidentStatus? status, string? tripId, int pageNumber, int pageSize, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Incident>>(Incidents.Where(i => i.UserId == userId && (!status.HasValue || i.Status == status) && (tripId is null || i.TripId == tripId)).ToArray()); public Task<long> CountByUserIdAsync(string userId, IncidentStatus? status, string? tripId, CancellationToken cancellationToken) => Task.FromResult((long)Incidents.Count(i => i.UserId == userId)); public Task UpdateAsync(Incident incident, CancellationToken cancellationToken) => Task.CompletedTask; }
}
