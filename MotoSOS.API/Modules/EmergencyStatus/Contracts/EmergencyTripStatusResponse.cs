namespace MotoSOS.API.Modules.EmergencyStatus.Contracts;

public sealed record EmergencyTripStatusResponse(string Id, string Status, DateTimeOffset StartedAtUtc, DateTimeOffset? FinishedAtUtc);
