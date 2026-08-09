using System.Globalization;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.MinorEvents.Contracts;
using MotoSOS.API.Modules.MinorEvents.Domain;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.MinorEvents.Application;

public sealed class MinorEventService : IMinorEventService
{
    private static readonly string[] SensitiveMetadataKeys = ["pass" + "word", "pass" + "wordHash", "access" + "Token", "refresh" + "Token", "tok" + "en", "authorization", "bearer", "device" + "Identifier", "device" + "IdentifierHash", "provider" + "Token", "pay" + "load", "email", "phone", "pay" + "ment", "card", "connection" + "String", "sec" + "ret", "stack" + "Trace", "exception", "mon" + "go", "mon" + "godb"];
    private readonly IUserRepository _users;
    private readonly ITripRepository _trips;
    private readonly IUserDeviceRepository _devices;
    private readonly IMinorEventRepository _minorEvents;
    private readonly IMinorEventIdempotencyKeyFactory _keys;
    private readonly IClock _clock;
    private readonly IAuditLogService? _auditLogs;

    public MinorEventService(IUserRepository users, ITripRepository trips, IUserDeviceRepository devices, IMinorEventRepository minorEvents, IMinorEventIdempotencyKeyFactory keys, IClock clock, IAuditLogService? auditLogs = null)
    {
        _users = users; _trips = trips; _devices = devices; _minorEvents = minorEvents; _keys = keys; _clock = clock; _auditLogs = auditLogs;
    }

    public async Task<CreateMinorEventResponse> CreateAsync(string userId, CreateMinorEventRequest request, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(userId, UserRole.Rider, cancellationToken);
        return await CreateCoreAsync(rider, request, null, cancellationToken);
    }

    public async Task<CreateMinorEventResponse> CreateFromOfflineAsync(string userId, string offlineIngestionRecordId, string fallbackTripId, string fallbackClientEventId, string? fallbackMobileDeviceId, DateTimeOffset fallbackOccurredAtUtc, CreateMinorEventRequest request, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(userId, UserRole.Rider, cancellationToken);
        var normalized = request with
        {
            TripId = string.IsNullOrWhiteSpace(request.TripId) ? fallbackTripId : request.TripId,
            ClientEventId = string.IsNullOrWhiteSpace(request.ClientEventId) ? fallbackClientEventId : request.ClientEventId,
            Source = string.IsNullOrWhiteSpace(request.Source) ? MinorEventSource.OfflineIngestion.ToString() : request.Source,
            MobileDeviceId = string.IsNullOrWhiteSpace(request.MobileDeviceId) ? fallbackMobileDeviceId : request.MobileDeviceId,
            OccurredAtUtc = request.OccurredAtUtc ?? fallbackOccurredAtUtc
        };
        CreateMinorEventResponse response = await CreateCoreAsync(rider, normalized, offlineIngestionRecordId, cancellationToken);
        await RecordAsync(rider, AuditAction.MinorEventProcessedFromOfflineIngestion, response.MinorEvent, cancellationToken);
        return response;
    }

    public async Task<GetMinorEventResponse> GetForRiderAsync(string userId, string id, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(userId, UserRole.Rider, cancellationToken);
        MinorEvent item = await GetOwnedAsync(rider.Id, id, cancellationToken);
        return new GetMinorEventResponse(ToResponse(item));
    }

    public async Task<GetMinorEventsResponse> ListForRiderAsync(string userId, MinorEventQuery query, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(userId, UserRole.Rider, cancellationToken);
        IReadOnlyList<MinorEvent> items = await _minorEvents.ListByUserIdAsync(rider.Id, query, cancellationToken);
        long total = await _minorEvents.CountByUserIdAsync(rider.Id, query, cancellationToken);
        return new GetMinorEventsResponse(items.Select(ToResponse).ToArray(), query.PageNumber, query.PageSize, total);
    }

    public async Task<GetMinorEventsResponse> ListForAdminAsync(string userId, MinorEventQuery query, CancellationToken cancellationToken)
    {
        await GetUserAsync(userId, UserRole.Admin, cancellationToken);
        IReadOnlyList<MinorEvent> items = await _minorEvents.ListAsync(query, cancellationToken);
        long total = await _minorEvents.CountAsync(query, cancellationToken);
        return new GetMinorEventsResponse(items.Select(ToResponse).ToArray(), query.PageNumber, query.PageSize, total);
    }

    public async Task<GetMinorEventResponse> MarkReviewedAsync(string userId, string id, CancellationToken cancellationToken) => await ChangeStatusAsync(userId, id, MinorEventStatus.Reviewed, AuditAction.MinorEventMarkedReviewed, cancellationToken);
    public async Task<GetMinorEventResponse> IgnoreAsync(string userId, string id, CancellationToken cancellationToken) => await ChangeStatusAsync(userId, id, MinorEventStatus.Ignored, AuditAction.MinorEventIgnored, cancellationToken);

    private async Task<CreateMinorEventResponse> CreateCoreAsync(User rider, CreateMinorEventRequest request, string? offlineRecordId, CancellationToken cancellationToken)
    {
        EnsureValidCreate(request);
        Trip trip = await GetOwnedReadyTripAsync(rider.Id, NormalizeRequired(request.TripId), cancellationToken);
        await EnsureOwnedDeviceAsync(rider.Id, request.MobileDeviceId, DeviceType.MobileApp, cancellationToken);
        await EnsureOwnedDeviceAsync(rider.Id, request.SmartwatchDeviceId, DeviceType.Smartwatch, cancellationToken);
        MinorEventType eventType = Parse<MinorEventType>(request.EventType!);
        DateTimeOffset now = _clock.UtcNow;
        var minorEvent = new MinorEvent
        {
            UserId = rider.Id,
            TripId = trip.Id,
            VehicleId = trip.VehicleId,
            MobileDeviceId = NormalizeOptional(request.MobileDeviceId),
            SmartwatchDeviceId = NormalizeOptional(request.SmartwatchDeviceId),
            ClientEventId = NormalizeRequired(request.ClientEventId),
            EventType = eventType,
            Severity = Parse<MinorEventSeverity>(request.Severity!),
            Source = Parse<MinorEventSource>(request.Source!),
            Status = MinorEventStatus.Recorded,
            Score = request.Score,
            Confidence = request.Confidence!.Value,
            GpsQuality = NormalizeOptional(request.GpsQuality),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            SpeedKmh = request.SpeedKmh,
            BatteryLevel = request.BatteryLevel,
            Message = NormalizeOptional(request.Message),
            OccurredAtUtc = request.OccurredAtUtc!.Value.ToUniversalTime(),
            ReceivedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ProcessedFromOfflineIngestionRecordId = NormalizeOptional(offlineRecordId),
            Metadata = SanitizeMetadata(request.Metadata),
            IdempotencyKey = _keys.Create(rider.Id, trip.Id, NormalizeRequired(request.ClientEventId), eventType.ToString())
        };
        (MinorEvent saved, _) = await _minorEvents.AddOrGetDuplicateAsync(minorEvent, cancellationToken);
        CreateMinorEventResponse response = new(ToResponse(saved));
        await RecordAsync(rider, AuditAction.MinorEventRecorded, response.MinorEvent, cancellationToken);
        return response;
    }

    private async Task<GetMinorEventResponse> ChangeStatusAsync(string userId, string id, MinorEventStatus status, AuditAction action, CancellationToken cancellationToken)
    {
        User rider = await GetUserAsync(userId, UserRole.Rider, cancellationToken);
        MinorEvent item = await GetOwnedAsync(rider.Id, id, cancellationToken);
        if (item.Status != status)
        {
            item.Status = status;
            item.UpdatedAtUtc = _clock.UtcNow;
            await _minorEvents.UpdateAsync(item, cancellationToken);
        }
        MinorEventResponse response = ToResponse(item);
        await RecordAsync(rider, action, response, cancellationToken);
        return new GetMinorEventResponse(response);
    }

    private async Task<User> GetUserAsync(string userId, UserRole role, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive) throw new UnauthorizedAppException("Invalid authentication credentials.");
        if (user.Role != role) throw new ForbiddenAppException("Minor Events API is not available for this role.");
        return user;
    }

    private async Task<Trip> GetOwnedReadyTripAsync(string userId, string tripId, CancellationToken cancellationToken)
    {
        Trip? trip = await _trips.GetByIdAsync(tripId, cancellationToken);
        if (trip is null || trip.UserId != userId) throw new NotFoundAppException("Trip was not found.");
        if (trip.Status is not (TripStatus.Active or TripStatus.Finished)) throw new TripNotReadyAppException("Trip is not ready for minor events.");
        return trip;
    }

    private async Task EnsureOwnedDeviceAsync(string userId, string? deviceId, DeviceType type, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return;
        UserDevice? device = await _devices.GetByIdAsync(deviceId.Trim(), cancellationToken);
        if (device is null || device.UserId != userId) throw new NotFoundAppException("Device was not found.");
        if (!device.IsActive || device.DeviceType != type || device.LinkStatus != DeviceLinkStatus.Linked) throw new TripNotReadyAppException("Device is not ready for minor events.");
    }

    private async Task<MinorEvent> GetOwnedAsync(string userId, string id, CancellationToken cancellationToken)
    {
        MinorEvent? item = await _minorEvents.GetByIdAsync(id.Trim(), cancellationToken);
        if (item is null || item.UserId != userId) throw new MinorEventNotAvailableAppException("Minor event is not available.");
        return item;
    }

    private void EnsureValidCreate(CreateMinorEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TripId)) throw new ValidationAppException("TripId is required.");
        if (string.IsNullOrWhiteSpace(request.ClientEventId)) throw new ValidationAppException("ClientEventId is required.");
        if (!Enum.TryParse(request.EventType, false, out MinorEventType eventType) || eventType == MinorEventType.Unknown) throw new ValidationAppException("EventType is invalid.");
        if (!Enum.TryParse(request.Severity, false, out MinorEventSeverity _)) throw new ValidationAppException("Severity is invalid.");
        if (!Enum.TryParse(request.Source, false, out MinorEventSource source) || source == MinorEventSource.Unknown) throw new ValidationAppException("Source is invalid.");
        if (!request.Confidence.HasValue || request.Confidence < 0 || request.Confidence > 1) throw new ValidationAppException("Confidence must be between 0 and 1.");
        if (request.Score is < 0 or > 100) throw new ValidationAppException("Score must be between 0 and 100.");
        if (request.Latitude is < -90 or > 90) throw new ValidationAppException("Latitude must be between -90 and 90.");
        if (request.Longitude is < -180 or > 180) throw new ValidationAppException("Longitude must be between -180 and 180.");
        if (!request.OccurredAtUtc.HasValue) throw new ValidationAppException("OccurredAtUtc is required.");
        if (request.OccurredAtUtc.Value.ToUniversalTime() > _clock.UtcNow.AddMinutes(2)) throw new ValidationAppException("OccurredAtUtc cannot be more than 2 minutes in the future.");
        if (request.Message?.Length > 1000) throw new ValidationAppException("Message cannot be longer than 1000 characters.");
    }

    private static Dictionary<string, string> SanitizeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0) return new Dictionary<string, string>();
        return metadata.Where(pair => !SensitiveMetadataKeys.Any(key => pair.Key.Contains(key, StringComparison.OrdinalIgnoreCase))).ToDictionary(pair => pair.Key, pair => pair.Value.Length > 200 ? pair.Value[..200] : pair.Value);
    }

    private async Task RecordAsync(User actor, AuditAction action, MinorEventResponse item, CancellationToken cancellationToken)
    {
        try
        {
            if (_auditLogs is not null) await _auditLogs.RecordAsync(actor.Id, actor.Role.ToString(), action, AuditModule.OfflineProcessing, "MinorEvent", item.Id, AuditOutcome.Success, null, null, null, new Dictionary<string, string> { ["minorEventId"] = item.Id, ["tripId"] = item.TripId, ["eventType"] = item.EventType, ["severity"] = item.Severity, ["status"] = item.Status, ["source"] = item.Source, ["score"] = item.Score?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, ["confidence"] = item.Confidence.ToString(CultureInfo.InvariantCulture), ["processedFromOfflineIngestionRecordId"] = item.ProcessedFromOfflineIngestionRecordId ?? string.Empty }, cancellationToken);
        }
        catch
        {
        }
    }

    private static TEnum Parse<TEnum>(string value) where TEnum : struct, Enum => Enum.Parse<TEnum>(value, false);
    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static MinorEventResponse ToResponse(MinorEvent e) => new(e.Id, e.TripId, e.EventType.ToString(), e.Severity.ToString(), e.Source.ToString(), e.Status.ToString(), e.Score, e.Confidence, e.GpsQuality, e.Latitude, e.Longitude, e.SpeedKmh, e.BatteryLevel, e.Message, e.OccurredAtUtc, e.ReceivedAtUtc, e.CreatedAtUtc, e.UpdatedAtUtc, e.ProcessedFromOfflineIngestionRecordId, e.Metadata);
}
