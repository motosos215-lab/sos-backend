namespace MotoSOS.API.Modules.Incidents.Contracts;

public sealed record IncidentLocationRequest(double? Latitude, double? Longitude, double? AccuracyMeters, double? SpeedKmh, string? Provider, DateTimeOffset? RecordedAtUtc);
