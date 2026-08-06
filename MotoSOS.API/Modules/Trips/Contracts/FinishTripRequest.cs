namespace MotoSOS.API.Modules.Trips.Contracts;

public sealed record FinishTripRequest(
    DateTimeOffset? ClientFinishedAtUtc,
    TripLocationRequest? EndLocation,
    int? BatteryLevel,
    string? Notes);
