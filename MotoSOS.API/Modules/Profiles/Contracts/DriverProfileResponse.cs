namespace MotoSOS.API.Modules.Profiles.Contracts;

public sealed record DriverProfileResponse(
    string? Id,
    string UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    DateOnly? DateOfBirth,
    string? CurpOrIdentifier,
    string? AddressOrZone,
    string? PrimaryCity,
    string? BloodType,
    string? Allergies,
    string? MedicalConditions,
    string? ProvisionalEmergencyContactName,
    string? ProvisionalEmergencyContactPhone,
    string LicenseDocumentStatus,
    string CompletionStatus,
    DateTimeOffset? CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc);
