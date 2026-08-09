namespace MotoSOS.API.Modules.Incidents.Contracts;

public sealed record CancelFalsePositiveRequest(string? Reason, DateTimeOffset? ClientCancelledAtUtc);
