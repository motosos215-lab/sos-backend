namespace MotoSOS.API.Modules.MinorEvents.Contracts;

public sealed record MinorEventResponse(string Id, string TripId, string EventType, string Severity, string Source, string Status, double? Score, double Confidence, string? GpsQuality, double? Latitude, double? Longitude, double? SpeedKmh, int? BatteryLevel, string? Message, DateTimeOffset OccurredAtUtc, DateTimeOffset ReceivedAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc, string? ProcessedFromOfflineIngestionRecordId, IReadOnlyDictionary<string, string> Metadata);
