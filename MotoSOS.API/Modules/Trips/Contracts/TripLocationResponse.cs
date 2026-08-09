namespace MotoSOS.API.Modules.Trips.Contracts;

public sealed record TripLocationResponse(
    double Latitude,
    double Longitude,
    double? AccuracyMeters,
    string? Provider,
    DateTimeOffset? RecordedAtUtc);
