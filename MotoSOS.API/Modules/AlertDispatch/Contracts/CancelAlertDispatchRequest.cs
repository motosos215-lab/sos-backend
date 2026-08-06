namespace MotoSOS.API.Modules.AlertDispatch.Contracts;

public sealed record CancelAlertDispatchRequest(string? Reason, DateTimeOffset? ClientCancelledAtUtc);
