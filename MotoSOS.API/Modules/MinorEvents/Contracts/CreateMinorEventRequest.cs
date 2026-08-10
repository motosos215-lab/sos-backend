namespace MotoSOS.API.Modules.MinorEvents.Contracts;

public sealed record CreateMinorEventRequest(string? TripId, string? ClientEventId, string? EventType, string? Severity, string? Source, string? MobileDeviceId, string? SmartwatchDeviceId, double? Score, double? Confidence, string? GpsQuality, double? Latitude, double? Longitude, double? SpeedKmh, int? BatteryLevel, string? Message, DateTimeOffset? OccurredAtUtc, IReadOnlyDictionary<string, string>? Metadata);
