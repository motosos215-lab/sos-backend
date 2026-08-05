using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.Profiles.Domain;

public sealed class DriverProfile
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string UserId { get; set; } = string.Empty;

    public string? DateOfBirth { get; set; }

    public string? CurpOrIdentifier { get; set; }

    public string? AddressOrZone { get; set; }

    public string? PrimaryCity { get; set; }

    public string? BloodType { get; set; }

    public string? Allergies { get; set; }

    public string? MedicalConditions { get; set; }

    public string? ProvisionalEmergencyContactName { get; set; }

    public string? ProvisionalEmergencyContactPhone { get; set; }

    public LicenseDocumentStatus LicenseDocumentStatus { get; set; } = LicenseDocumentStatus.NotUploaded;

    public ProfileCompletionStatus CompletionStatus { get; set; } = ProfileCompletionStatus.Draft;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
