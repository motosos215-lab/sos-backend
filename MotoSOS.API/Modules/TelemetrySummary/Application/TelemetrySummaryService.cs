using System.Globalization;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.MinorEvents.Application;
using MotoSOS.API.Modules.MinorEvents.Domain;
using MotoSOS.API.Modules.TelemetrySummary.Contracts;
using MotoSOS.API.Modules.TelemetrySummary.Domain;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.TelemetrySummary.Application;

public sealed class TelemetrySummaryService : ITelemetrySummaryService
{
    private readonly IUserRepository _users;
    private readonly ITripRepository _trips;
    private readonly IMinorEventRepository _minorEvents;
    private readonly ITelemetrySummaryRepository _summaries;
    private readonly IClock _clock;
    private readonly IAuditLogService? _auditLogs;

    public TelemetrySummaryService(IUserRepository users, ITripRepository trips, IMinorEventRepository minorEvents, ITelemetrySummaryRepository summaries, IClock clock, IAuditLogService? auditLogs = null)
    {
        _users = users; _trips = trips; _minorEvents = minorEvents; _summaries = summaries; _clock = clock; _auditLogs = auditLogs;
    }

    public async Task<TelemetrySummaryResponse> GetForRiderAsync(string userId, string tripId, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(userId, UserRole.Rider, cancellationToken);
        Trip trip = await GetOwnedReadyTripAsync(rider.Id, tripId, cancellationToken);
        TripTelemetrySummary? existing = await _summaries.GetByUserIdAndTripIdAsync(rider.Id, trip.Id, cancellationToken);
        if (existing is not null) return ToResponse(existing);
        return ToResponse(await ComputeAndSaveAsync(trip, AuditAction.TelemetrySummaryComputed, cancellationToken));
    }

    public async Task<TelemetrySummaryResponse> RecomputeForRiderAsync(string userId, string tripId, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(userId, UserRole.Rider, cancellationToken);
        Trip trip = await GetOwnedReadyTripAsync(rider.Id, tripId, cancellationToken);
        return ToResponse(await ComputeAndSaveAsync(trip, AuditAction.TelemetrySummaryRecomputed, cancellationToken));
    }

    public async Task<GetTelemetrySummariesResponse> ListForAdminAsync(string adminUserId, TelemetrySummaryQuery query, CancellationToken cancellationToken)
    {
        await GetUserAsync(adminUserId, UserRole.Admin, cancellationToken);
        IReadOnlyList<TripTelemetrySummary> items = await _summaries.ListAsync(query, cancellationToken);
        long total = await _summaries.CountAsync(query, cancellationToken);
        return new GetTelemetrySummariesResponse(items.Select(ToResponse).ToArray(), query.PageNumber, query.PageSize, total);
    }

    public async Task<TelemetrySummaryResponse> GetForAdminAsync(string adminUserId, string id, CancellationToken cancellationToken)
    {
        await GetUserAsync(adminUserId, UserRole.Admin, cancellationToken);
        TripTelemetrySummary summary = await _summaries.GetByIdAsync(id.Trim(), cancellationToken) ?? throw new TelemetrySummaryNotAvailableAppException("Telemetry summary is not available.");
        return ToResponse(summary);
    }

    private async Task<TripTelemetrySummary> ComputeAndSaveAsync(Trip trip, AuditAction action, CancellationToken cancellationToken)
    {
        IReadOnlyList<MinorEvent> events = await _minorEvents.ListByTripIdAsync(trip.UserId, trip.Id, cancellationToken);
        TripTelemetrySummary? existing = await _summaries.GetByUserIdAndTripIdAsync(trip.UserId, trip.Id, cancellationToken);
        DateTimeOffset now = _clock.UtcNow;
        TripTelemetrySummary summary = BuildSummary(trip, events, existing?.CreatedAtUtc ?? now, now);
        if (existing is not null) summary.Id = existing.Id;
        TripTelemetrySummary saved = await _summaries.UpsertAsync(summary, cancellationToken);
        await RecordAsync(action, saved, cancellationToken);
        return saved;
    }

    private static TripTelemetrySummary BuildSummary(Trip trip, IReadOnlyList<MinorEvent> events, DateTimeOffset createdAtUtc, DateTimeOffset now)
    {
        var summary = new TripTelemetrySummary
        {
            UserId = trip.UserId,
            TripId = trip.Id,
            VehicleId = trip.VehicleId,
            TripStatus = trip.Status,
            SummaryStatus = events.Count == 0 ? TelemetrySummaryStatus.NoData : TelemetrySummaryStatus.Computed,
            TotalMinorEvents = events.Count,
            EventsByType = CountBy(events, e => e.EventType.ToString()),
            EventsBySeverity = CountBy(events, e => e.Severity.ToString()),
            EventsBySource = CountBy(events, e => e.Source.ToString()),
            EventsByStatus = CountBy(events, e => e.Status.ToString()),
            HardBrakeCount = CountType(events, MinorEventType.HardBrake),
            HarshAccelerationCount = CountType(events, MinorEventType.HarshAcceleration),
            SharpTurnCount = CountType(events, MinorEventType.SharpTurn),
            GpsSignalLostCount = CountType(events, MinorEventType.GpsSignalLost),
            GpsSignalRecoveredCount = CountType(events, MinorEventType.GpsSignalRecovered),
            LowBatteryCount = CountType(events, MinorEventType.LowBattery),
            SmartwatchDisconnectedCount = CountType(events, MinorEventType.SmartwatchDisconnected),
            SmartwatchReconnectedCount = CountType(events, MinorEventType.SmartwatchReconnected),
            SensorAnomalyCount = CountType(events, MinorEventType.SensorAnomaly),
            PossibleFallLowConfidenceCount = CountType(events, MinorEventType.PossibleFallLowConfidence),
            InformationalCount = CountType(events, MinorEventType.Informational),
            FirstEventAtUtc = events.Count == 0 ? null : events.Min(e => e.OccurredAtUtc),
            LastEventAtUtc = events.Count == 0 ? null : events.Max(e => e.OccurredAtUtc),
            AverageConfidence = events.Count == 0 ? null : events.Average(e => e.Confidence),
            MaxScore = MaxOrNull(events.Select(e => e.Score)),
            AverageScore = AverageOrNull(events.Select(e => e.Score)),
            MinBatteryLevel = MinOrNull(events.Select(e => e.BatteryLevel)),
            MaxSpeedKmh = MaxOrNull(events.Select(e => e.SpeedKmh)),
            GpsQualitySamples = events.Where(e => !string.IsNullOrWhiteSpace(e.GpsQuality)).GroupBy(e => e.GpsQuality!.Trim()).ToDictionary(g => g.Key, g => g.Count()),
            LastComputedAtUtc = now,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = now
        };
        return summary;
    }

    private async Task<Trip> GetOwnedReadyTripAsync(string userId, string tripId, CancellationToken cancellationToken)
    {
        Trip? trip = await _trips.GetByIdAsync(tripId.Trim(), cancellationToken);
        if (trip is null || trip.UserId != userId) throw new NotFoundAppException("Trip was not found.");
        if (trip.Status is not (TripStatus.Active or TripStatus.Finished)) throw new TripNotReadyAppException("Trip is not ready for telemetry summary.");
        return trip;
    }

    private async Task<User> GetUserAsync(string userId, UserRole role, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != role) throw new ForbiddenAppException("Telemetry Summary API is not available for this role.");
        return user;
    }

    private async Task RecordAsync(AuditAction action, TripTelemetrySummary summary, CancellationToken cancellationToken)
    {
        try
        {
            if (_auditLogs is not null) await _auditLogs.RecordAsync(summary.UserId, UserRole.Rider.ToString(), action, AuditModule.OfflineProcessing, "TripTelemetrySummary", summary.Id, AuditOutcome.Success, null, null, null, new Dictionary<string, string> { ["telemetrySummaryId"] = summary.Id, ["tripId"] = summary.TripId, ["totalMinorEvents"] = summary.TotalMinorEvents.ToString(CultureInfo.InvariantCulture), ["summaryStatus"] = summary.SummaryStatus.ToString(), ["lastComputedAtUtc"] = summary.LastComputedAtUtc.ToString("O", CultureInfo.InvariantCulture) }, cancellationToken);
        }
        catch
        {
        }
    }

    private static Dictionary<string, int> CountBy(IReadOnlyList<MinorEvent> events, Func<MinorEvent, string> keySelector) => events.GroupBy(keySelector).ToDictionary(g => g.Key, g => g.Count());
    private static int CountType(IReadOnlyList<MinorEvent> events, MinorEventType type) => events.Count(e => e.EventType == type);
    private static double? AverageOrNull(IEnumerable<double?> values) { double[] available = values.Where(v => v.HasValue).Select(v => v!.Value).ToArray(); return available.Length == 0 ? null : available.Average(); }
    private static double? MaxOrNull(IEnumerable<double?> values) { double[] available = values.Where(v => v.HasValue).Select(v => v!.Value).ToArray(); return available.Length == 0 ? null : available.Max(); }
    private static int? MinOrNull(IEnumerable<int?> values) { int[] available = values.Where(v => v.HasValue).Select(v => v!.Value).ToArray(); return available.Length == 0 ? null : available.Min(); }
    private static TelemetrySummaryResponse ToResponse(TripTelemetrySummary s) => new(s.Id, s.UserId, s.TripId, s.VehicleId, s.TripStatus.ToString(), s.SummaryStatus.ToString(), s.TotalMinorEvents, s.EventsByType, s.EventsBySeverity, s.EventsBySource, s.EventsByStatus, s.HardBrakeCount, s.HarshAccelerationCount, s.SharpTurnCount, s.GpsSignalLostCount, s.GpsSignalRecoveredCount, s.LowBatteryCount, s.SmartwatchDisconnectedCount, s.SmartwatchReconnectedCount, s.SensorAnomalyCount, s.PossibleFallLowConfidenceCount, s.InformationalCount, s.FirstEventAtUtc, s.LastEventAtUtc, s.AverageConfidence, s.MaxScore, s.AverageScore, s.MinBatteryLevel, s.MaxSpeedKmh, s.GpsQualitySamples, s.LastComputedAtUtc, s.CreatedAtUtc, s.UpdatedAtUtc);
}
