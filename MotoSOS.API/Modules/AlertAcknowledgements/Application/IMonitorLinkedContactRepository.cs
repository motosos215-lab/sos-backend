using MotoSOS.API.Modules.EmergencyContacts.Domain;

namespace MotoSOS.API.Modules.AlertAcknowledgements.Application;

public interface IMonitorLinkedContactRepository
{
    Task<IReadOnlyList<EmergencyContact>> GetActiveLinkedByLinkedUserIdAsync(string linkedUserId, CancellationToken cancellationToken);
}
