using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoOnboardingConfirmationRepository : IOnboardingConfirmationRepository
{
    private readonly IMongoCollection<OnboardingConfirmation> _confirmations;

    public MongoOnboardingConfirmationRepository(IMongoDatabase database)
    {
        _confirmations = database.GetCollection<OnboardingConfirmation>(MongoCollectionNames.OnboardingConfirmations);
    }

    public async Task<OnboardingConfirmation?> GetByUserIdAsync(string userId, CancellationToken cancellationToken) =>
        await _confirmations.Find(confirmation => confirmation.UserId == userId).FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) =>
        await _confirmations.InsertOneAsync(confirmation, cancellationToken: cancellationToken);

    public async Task UpdateAsync(OnboardingConfirmation confirmation, CancellationToken cancellationToken) =>
        await _confirmations.ReplaceOneAsync(existing => existing.Id == confirmation.Id, confirmation, cancellationToken: cancellationToken);
}
