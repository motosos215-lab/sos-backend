using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredEmergencyContactRepository : IEmergencyContactRepository
{
    public Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => throw CreateException();
    public Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw CreateException();
    public Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken cancellationToken) => throw CreateException();
    public Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => throw CreateException();
    public Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken) => throw CreateException();
    public Task UpdateAsync(EmergencyContact contact, CancellationToken cancellationToken) => throw CreateException();

    private static InvalidOperationException CreateException() => new("MongoDB is not configured for emergency contact persistence.");
}
