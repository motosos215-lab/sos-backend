namespace MotoSOS.API.Modules.Vehicles.Contracts;

public sealed record GetVehiclesResponse(IReadOnlyList<VehicleResponse> Vehicles);
