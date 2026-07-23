using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class UnconfiguredUserRepository : IUserRepository
{
    public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken) => throw CreateException();

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => throw CreateException();

    public Task AddAsync(User user, CancellationToken cancellationToken) => throw CreateException();

    public Task UpdateAsync(User user, CancellationToken cancellationToken) => throw CreateException();

    private static InvalidOperationException CreateException() => new("MongoDB is not configured for user persistence.");
}
