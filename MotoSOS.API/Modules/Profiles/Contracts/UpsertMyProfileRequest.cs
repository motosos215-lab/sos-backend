namespace MotoSOS.API.Modules.Profiles.Contracts;

public sealed record UpsertMyProfileRequest(
    string? FullName,
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
    string? SaveMode);
