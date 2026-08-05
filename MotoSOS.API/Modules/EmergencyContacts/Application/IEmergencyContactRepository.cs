using MotoSOS.API.Modules.EmergencyContacts.Domain;

namespace MotoSOS.API.Modules.EmergencyContacts.Application;

public interface IEmergencyContactRepository
{
    Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken);

    Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken cancellationToken);

    Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken);

    Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken);

    Task UpdateAsync(EmergencyContact contact, CancellationToken cancellationToken);
}
