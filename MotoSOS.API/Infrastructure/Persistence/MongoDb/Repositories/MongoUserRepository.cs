using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoUserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;

    public MongoUserRepository(IMongoDatabase database)
    {
        _users = database.GetCollection<User>(MongoCollectionNames.Users);
    }

    public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return await _users.Find(user => user.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        string normalizedEmail = email.Trim().ToLowerInvariant();
        return await _users.Find(user => user.Email == normalizedEmail).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await _users.InsertOneAsync(user, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        await _users.ReplaceOneAsync(existing => existing.Id == user.Id, user, cancellationToken: cancellationToken);
    }
}
