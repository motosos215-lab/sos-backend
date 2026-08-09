namespace MotoSOS.API.Modules.ReportExports.Contracts;

public sealed record ResolutionReportTripSummaryResponse(string? TripId, string? TripStatus, string? VehicleId, DateTimeOffset? StartedAtUtc, DateTimeOffset? FinishedAtUtc);
