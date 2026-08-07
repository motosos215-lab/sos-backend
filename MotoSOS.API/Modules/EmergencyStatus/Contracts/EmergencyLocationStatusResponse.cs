namespace MotoSOS.API.Modules.EmergencyStatus.Contracts;

public sealed record EmergencyLocationStatusResponse(bool Available, string? IncidentId, string? TripId, double? Latitude, double? Longitude, double? AccuracyMeters, string? Source, DateTimeOffset? RecordedAtUtc, DateTimeOffset? ReceivedAtUtc, bool? IsActive, bool? IsStale);
