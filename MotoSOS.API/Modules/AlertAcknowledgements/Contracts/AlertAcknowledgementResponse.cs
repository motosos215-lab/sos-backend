namespace MotoSOS.API.Modules.AlertAcknowledgements.Contracts;

public sealed record AlertAcknowledgementResponse(string Id, string AlertDispatchId, string NotificationDeliveryAttemptId, string IncidentId, string TripId, string EmergencyContactId, string Status, string ResponseType, string? Message, DateTimeOffset? ViewedAtUtc, DateTimeOffset? AcknowledgedAtUtc, DateTimeOffset? DeclinedAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);
