using MotoSOS.API.Modules.Vehicles.Domain;

namespace MotoSOS.API.Modules.Vehicles.Application;

public interface IDriverVehicleRepository
{
    Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken);

    Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken);

    Task AddAsync(DriverVehicle vehicle, CancellationToken cancellationToken);

    Task UpdateAsync(DriverVehicle vehicle, CancellationToken cancellationToken);
}
