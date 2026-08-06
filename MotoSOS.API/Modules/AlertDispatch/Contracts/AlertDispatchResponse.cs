namespace MotoSOS.API.Modules.AlertDispatch.Contracts;

public sealed record AlertDispatchResponse(string Id, string IncidentId, string TripId, string VehicleId, string MobileDeviceId, string? SmartwatchDeviceId, string Priority, string Reason, string Status, DateTimeOffset RequestedAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc, DateTimeOffset? CancelledAtUtc, DateTimeOffset? CompletedAtUtc, string? Notes, int ContactsCount);
