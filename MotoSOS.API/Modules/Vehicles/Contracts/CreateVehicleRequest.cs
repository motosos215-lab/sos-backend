namespace MotoSOS.API.Modules.Vehicles.Contracts;

public sealed record CreateVehicleRequest(
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
    string? SaveMode);
