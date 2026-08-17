namespace MotoSOS.API.Modules.Trips.Contracts;

public sealed record TripRoutePointResponse(
    string Id,
    string ClientRoutePointId,
    int Sequence,
    DateTimeOffset RecordedAtUtc,
    double Latitude,
    double Longitude,
    double AccuracyMeters,
    double? SpeedMetersPerSecond,
    double? BearingDegrees);
