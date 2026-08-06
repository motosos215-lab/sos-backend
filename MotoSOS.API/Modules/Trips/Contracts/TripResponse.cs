namespace MotoSOS.API.Modules.Trips.Contracts;

public sealed record TripResponse(
    string Id,
    string UserId,
    string VehicleId,
    string MobileDeviceId,
    string? SmartwatchDeviceId,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    DateTimeOffset? ClientStartedAtUtc,
    DateTimeOffset? ClientFinishedAtUtc,
    TripLocationResponse? StartLocation,
    TripLocationResponse? EndLocation,
    int? StartBatteryLevel,
    int? EndBatteryLevel,
    string? AppVersion,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
