namespace MotoSOS.API.Modules.Vehicles.Contracts;

public sealed record VehicleResponse(
    string Id,
    string UserId,
    string? VehicleType,
    string? Brand,
    string? Model,
    int? Year,
    string? Alias,
    string? PrimaryUse,
    string? Color,
    string? PlateNumber,
    string? Vin,
    string? UsageFrequency,
    string CompletionStatus,
    bool IsPrimary,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc);
