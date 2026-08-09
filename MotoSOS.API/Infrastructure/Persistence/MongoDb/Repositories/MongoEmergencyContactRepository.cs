using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoEmergencyContactRepository : IEmergencyContactRepository, IMonitorLinkedContactRepository
{
    private readonly IMongoCollection<EmergencyContact> _contacts;

    public MongoEmergencyContactRepository(IMongoDatabase database)
    {
        _contacts = database.GetCollection<EmergencyContact>(MongoCollectionNames.EmergencyContacts);
    }

    public async Task<IReadOnlyList<EmergencyContact>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) =>
        await _contacts.Find(contact => contact.UserId == userId && contact.IsActive).ToListAsync(cancellationToken);

    public async Task<EmergencyContact?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        await _contacts.Find(contact => contact.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<EmergencyContact?> GetByLinkingCodeAsync(string linkingCode, CancellationToken cancellationToken) =>
        await _contacts.Find(contact => contact.LinkingCode == linkingCode).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<EmergencyContact>> GetActiveLinkedByLinkedUserIdAsync(string linkedUserId, CancellationToken cancellationToken) =>
        await _contacts.Find(contact => contact.LinkedUserId == linkedUserId && contact.IsActive && contact.InvitationStatus == EmergencyContactInvitationStatus.Linked).ToListAsync(cancellationToken);

    public async Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        long count = await _contacts.CountDocumentsAsync(contact => contact.UserId == userId && contact.IsActive, cancellationToken: cancellationToken);
        return (int)count;
    }

    public async Task AddAsync(EmergencyContact contact, CancellationToken cancellationToken) =>
        await _contacts.InsertOneAsync(contact, cancellationToken: cancellationToken);

    public async Task UpdateAsync(EmergencyContact contact, CancellationToken cancellationToken) =>
        await _contacts.ReplaceOneAsync(existing => existing.Id == contact.Id, contact, cancellationToken: cancellationToken);
}
