using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredMonitorLinkedContactRepository : IMonitorLinkedContactRepository
{
    public Task<IReadOnlyList<EmergencyContact>> GetActiveLinkedByLinkedUserIdAsync(string linkedUserId, CancellationToken cancellationToken) => throw new InvalidOperationException("MongoDB is not configured. Configure MongoDb:ConnectionString and MongoDb:DatabaseName to use Alert Acknowledgements API.");
}
