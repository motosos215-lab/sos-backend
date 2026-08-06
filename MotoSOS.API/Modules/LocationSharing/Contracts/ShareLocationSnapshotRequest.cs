namespace MotoSOS.API.Modules.LocationSharing.Contracts;

public sealed record ShareLocationSnapshotRequest(string? IncidentId, string? ClientLocationUpdateId, double? Latitude, double? Longitude, double? AccuracyMeters, double? AltitudeMeters, double? SpeedMetersPerSecond, double? HeadingDegrees, int? BatteryPercentage, string? Source, DateTimeOffset? RecordedAtUtc);
