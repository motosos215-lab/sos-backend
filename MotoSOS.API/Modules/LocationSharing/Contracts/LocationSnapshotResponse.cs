namespace MotoSOS.API.Modules.LocationSharing.Contracts;

public sealed record LocationSnapshotResponse(string IncidentId, string TripId, double Latitude, double Longitude, double? AccuracyMeters, string Source, DateTimeOffset RecordedAtUtc, DateTimeOffset ReceivedAtUtc, bool IsActive, bool IsStale);
