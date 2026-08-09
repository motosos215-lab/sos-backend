namespace MotoSOS.API.Modules.Trips.Contracts;

public sealed record StartTripRequest(
    string? VehicleId,
    string? MobileDeviceId,
    string? SmartwatchDeviceId,
    DateTimeOffset? ClientStartedAtUtc,
    TripLocationRequest? StartLocation,
    int? BatteryLevel,
    string? AppVersion);
