using MotoSOS.API.Modules.Profiles.Domain;

namespace MotoSOS.API.Modules.Profiles.Application;

public interface IDriverProfileRepository
{
    Task<DriverProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken);

    Task AddAsync(DriverProfile profile, CancellationToken cancellationToken);

    Task UpdateAsync(DriverProfile profile, CancellationToken cancellationToken);
}
