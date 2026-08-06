namespace MotoSOS.API.Modules.Incidents.Contracts;

public sealed record CloseIncidentRequest(string? ClosureReason, string? ClosureNotes, DateTimeOffset? ClientClosedAtUtc);
