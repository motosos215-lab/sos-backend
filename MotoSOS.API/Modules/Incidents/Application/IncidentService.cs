using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Incidents.Contracts;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.Incidents.Application;

public sealed class IncidentService : IIncidentService
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IUserRepository _users;
    private readonly IOnboardingService _onboarding;
    private readonly ITripRepository _trips;
    private readonly IIncidentRepository _incidents;
    private readonly IIncidentIdempotencyKeyFactory _idempotencyKeys;
    private readonly IClock _clock;

    public IncidentService(IUserRepository users, IOnboardingService onboarding, ITripRepository trips, IIncidentRepository incidents, IIncidentIdempotencyKeyFactory idempotencyKeys, IClock clock)
    {
        _users = users;
        _onboarding = onboarding;
        _trips = trips;
        _incidents = incidents;
        _idempotencyKeys = idempotencyKeys;
        _clock = clock;
    }

    public async Task<CreateIncidentResponse> CreateAsync(string userId, CreateIncidentRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        await EnsureOnboardingReadyAsync(user.Id, cancellationToken);
        Trip trip = await GetOwnedTripAsync(user.Id, NormalizeRequired(request.TripId), cancellationToken);
        string idempotencyKey = _idempotencyKeys.Create(user.Id, trip.Id, NormalizeRequired(request.ClientIncidentId));
        DateTimeOffset now = _clock.UtcNow;
        var incident = new Incident
        {
            UserId = user.Id,
            TripId = trip.Id,
            VehicleId = trip.VehicleId,
            MobileDeviceId = trip.MobileDeviceId,
            SmartwatchDeviceId = trip.SmartwatchDeviceId,
            ClientIncidentId = NormalizeRequired(request.ClientIncidentId),
            IdempotencyKey = idempotencyKey,
            Source = ParseEnum<IncidentSource>(request.Source)!.Value,
            Cause = ParseEnum<IncidentCause>(request.Cause)!.Value,
            RiskLevel = ParseEnum<IncidentRiskLevel>(request.RiskLevel)!.Value,
            Status = IncidentStatus.Open,
            Score = request.Score,
            Confidence = request.Confidence,
            GpsQuality = NormalizeOptional(request.GpsQuality),
            RuleSetVersion = NormalizeOptional(request.RuleSetVersion),
            ValidationPolicyVersion = NormalizeOptional(request.ValidationPolicyVersion),
            Location = ToLocation(request.Location),
            EvidenceSummary = ToEvidence(request.EvidenceSummary),
            OccurredAtUtc = request.OccurredAtUtc!.Value,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        (Incident persisted, _) = await _incidents.AddOrGetDuplicateAsync(incident, cancellationToken);
        return new CreateIncidentResponse(ToResponse(persisted));
    }

    public async Task<GetIncidentsResponse> ListAsync(string userId, string? status, string? tripId, int? pageNumber, int? pageSize, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        IncidentStatus? parsedStatus = ParseEnum<IncidentStatus>(status);
        int normalizedPageNumber = Math.Max(pageNumber ?? DefaultPageNumber, 1);
        int normalizedPageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        string? normalizedTripId = NormalizeOptional(tripId);
        IReadOnlyList<Incident> incidents = await _incidents.ListByUserIdAsync(user.Id, parsedStatus, normalizedTripId, normalizedPageNumber, normalizedPageSize, cancellationToken);
        long totalCount = await _incidents.CountByUserIdAsync(user.Id, parsedStatus, normalizedTripId, cancellationToken);
        return new GetIncidentsResponse(incidents.Select(ToResponse).ToArray(), normalizedPageNumber, normalizedPageSize, totalCount);
    }

    public async Task<GetIncidentResponse> GetAsync(string userId, string incidentId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        Incident incident = await GetOwnedIncidentAsync(user.Id, incidentId, cancellationToken);
        return new GetIncidentResponse(ToResponse(incident));
    }

    public async Task<CancelFalsePositiveResponse> CancelFalsePositiveAsync(string userId, string incidentId, CancelFalsePositiveRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        Incident incident = await GetOwnedIncidentAsync(user.Id, incidentId, cancellationToken);
        if (incident.Status == IncidentStatus.Closed) throw new IncidentAlreadyClosedAppException("Incident is already closed.");
        if (incident.Status == IncidentStatus.FalsePositiveCancelled) return new CancelFalsePositiveResponse(ToResponse(incident));

        DateTimeOffset now = _clock.UtcNow;
        incident.Status = IncidentStatus.FalsePositiveCancelled;
        incident.CancelledAtUtc = now;
        incident.ClosureReason = NormalizeOptional(request.Reason);
        incident.UpdatedAtUtc = now;
        await _incidents.UpdateAsync(incident, cancellationToken);
        return new CancelFalsePositiveResponse(ToResponse(incident));
    }

    public async Task<CloseIncidentResponse> CloseAsync(string userId, string incidentId, CloseIncidentRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        Incident incident = await GetOwnedIncidentAsync(user.Id, incidentId, cancellationToken);
        if (incident.Status == IncidentStatus.Closed) return new CloseIncidentResponse(ToResponse(incident));

        DateTimeOffset now = _clock.UtcNow;
        incident.Status = IncidentStatus.Closed;
        incident.ClosedAtUtc = now;
        incident.ClosedByUserId = user.Id;
        incident.ClosureReason = NormalizeOptional(request.ClosureReason);
        incident.ClosureNotes = NormalizeOptional(request.ClosureNotes);
        incident.UpdatedAtUtc = now;
        await _incidents.UpdateAsync(incident, cancellationToken);
        return new CloseIncidentResponse(ToResponse(incident));
    }

    private async Task<User> GetRiderUserAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != UserRole.Rider) throw new ForbiddenAppException("Incidents API is available only for riders.");
        return user;
    }

    private async Task EnsureOnboardingReadyAsync(string userId, CancellationToken cancellationToken)
    {
        OnboardingStatusResponse status = await _onboarding.GetStatusAsync(userId, cancellationToken);
        if (status.CompletedSteps != 7 || status.CurrentStep != "Completed" || !status.IsOperational) throw new OnboardingNotReadyAppException("Onboarding must be completed before creating incidents.");
    }

    private async Task<Trip> GetOwnedTripAsync(string userId, string tripId, CancellationToken cancellationToken)
    {
        Trip? trip = await _trips.GetByIdAsync(tripId, cancellationToken);
        if (trip is null || trip.UserId != userId) throw new NotFoundAppException("Trip was not found.");
        if (trip.Status is not (TripStatus.Active or TripStatus.Finished)) throw new TripNotReadyAppException("Trip is not ready for incidents.");
        return trip;
    }

    private async Task<Incident> GetOwnedIncidentAsync(string userId, string incidentId, CancellationToken cancellationToken)
    {
        Incident? incident = await _incidents.GetByIdAsync(incidentId, cancellationToken);
        if (incident is null || incident.UserId != userId) throw new NotFoundAppException("Incident was not found.");
        return incident;
    }

    private static IncidentResponse ToResponse(Incident incident) => new(incident.Id, incident.TripId, incident.VehicleId, incident.MobileDeviceId, incident.SmartwatchDeviceId, incident.Source.ToString(), incident.Cause.ToString(), incident.RiskLevel.ToString(), incident.Status.ToString(), incident.Score, incident.Confidence, incident.OccurredAtUtc, incident.CreatedAtUtc, incident.UpdatedAtUtc, incident.CancelledAtUtc, incident.ClosedAtUtc, incident.ClosureReason, incident.ClosureNotes);
    private static IncidentLocation? ToLocation(IncidentLocationRequest? request) => request is null ? null : new IncidentLocation { Latitude = request.Latitude!.Value, Longitude = request.Longitude!.Value, AccuracyMeters = request.AccuracyMeters, SpeedKmh = request.SpeedKmh, Provider = NormalizeOptional(request.Provider), RecordedAtUtc = request.RecordedAtUtc };
    private static IncidentEvidenceSummary? ToEvidence(IncidentEvidenceSummaryRequest? request) => request is null ? null : new IncidentEvidenceSummary { AssessmentId = NormalizeOptional(request.AssessmentId), WindowId = NormalizeOptional(request.WindowId), TriggeredRules = request.TriggeredRules?.Select(rule => rule.Trim()).Where(rule => rule.Length > 0).ToArray() ?? [], HasSmartwatchData = request.HasSmartwatchData, HasLocation = request.HasLocation, PhoneBatteryLevel = request.PhoneBatteryLevel, WatchBatteryLevel = request.WatchBatteryLevel, AppVersion = NormalizeOptional(request.AppVersion) };
    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct => Enum.TryParse(value, ignoreCase: true, out TEnum result) ? result : null;
    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
