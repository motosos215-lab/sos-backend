using MotoSOS.API.Modules.Vehicles.Contracts;

namespace MotoSOS.API.Modules.Vehicles.Application;

public interface IVehicleService
{
    Task<GetVehiclesResponse> GetMyVehiclesAsync(string userId, CancellationToken cancellationToken);

    Task<GetVehicleResponse> GetMyVehicleAsync(string userId, string vehicleId, CancellationToken cancellationToken);

    Task<CreateVehicleResponse> CreateMyVehicleAsync(string userId, CreateVehicleRequest request, CancellationToken cancellationToken);

    Task<UpdateVehicleResponse> UpdateMyVehicleAsync(string userId, string vehicleId, UpdateVehicleRequest request, CancellationToken cancellationToken);

    Task DeleteMyVehicleAsync(string userId, string vehicleId, CancellationToken cancellationToken);
}
