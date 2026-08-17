namespace MotoSOS.API.Modules.Trips.Contracts;

public sealed record CreateTripRoutePointRequest(
    string? ClientRoutePointId,
    int? Sequence,
    DateTimeOffset? RecordedAtUtc,
    double? Latitude,
    double? Longitude,
    double? AccuracyMeters,
    double? SpeedMetersPerSecond,
    double? BearingDegrees,
    string? AppVersion = null,
    string? DeviceId = null);
