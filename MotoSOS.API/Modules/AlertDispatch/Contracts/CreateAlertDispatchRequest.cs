namespace MotoSOS.API.Modules.AlertDispatch.Contracts;

public sealed record CreateAlertDispatchRequest(string? IncidentId, string? ClientAlertRequestId, string? Priority, string? Reason, DateTimeOffset? RequestedAtUtc, string? Notes);
