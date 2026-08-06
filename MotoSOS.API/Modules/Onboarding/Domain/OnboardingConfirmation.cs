using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.Onboarding.Domain;

public sealed class OnboardingConfirmation
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset ConfirmedAtUtc { get; set; }

    public bool IsOperational { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
