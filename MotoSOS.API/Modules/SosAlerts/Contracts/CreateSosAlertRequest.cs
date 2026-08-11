namespace MotoSOS.API.Modules.SosAlerts.Contracts;

public sealed record CreateSosAlertRequest(
    string? TripId,
    string? ClientIncidentId,
    string? ClientAlertRequestId,
    string? IncidentType,
    string? Severity,
    DateTimeOffset? DetectedAtUtc,
    double? Latitude,
    double? Longitude,
    string? Priority,
    string? Reason,
    string? Notes);
