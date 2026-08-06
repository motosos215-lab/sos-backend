namespace MotoSOS.API.Modules.Plans.Contracts;

public sealed record SubscriptionResponse(
    string Id,
    string UserId,
    string PlanTier,
    string Status,
    string Source,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset? CancelledAtUtc,
    DateTimeOffset? ConfirmedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
