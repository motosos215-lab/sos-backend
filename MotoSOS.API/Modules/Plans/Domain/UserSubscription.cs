using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.Plans.Domain;

public sealed class UserSubscription
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string UserId { get; set; } = string.Empty;
    public PlanTier PlanTier { get; set; } = PlanTier.Basic;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public SubscriptionSource Source { get; set; } = SubscriptionSource.WebBasic;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
}
