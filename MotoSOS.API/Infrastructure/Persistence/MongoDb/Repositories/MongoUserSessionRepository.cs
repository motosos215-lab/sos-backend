using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Auth.Sessions.Application;
using MotoSOS.API.Modules.Auth.Sessions.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoUserSessionRepository : IUserSessionRepository
{
    private readonly IMongoCollection<UserSession> _sessions;

    public MongoUserSessionRepository(IMongoDatabase database) => _sessions = database.GetCollection<UserSession>(MongoCollectionNames.UserSessions);

    public async Task<UserSession?> GetByIdAsync(string id, CancellationToken cancellationToken) => await _sessions.Find(session => session.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<UserSession?> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) => await _sessions.Find(session => session.UserId == userId && session.RevokedAtUtc == null).FirstOrDefaultAsync(cancellationToken);

    public async Task<UserSession?> GetActiveByUserIdAndSessionTypeAsync(string userId, UserSessionType sessionType, CancellationToken cancellationToken) =>
        await _sessions.Find(session => session.UserId == userId && session.SessionType == sessionType && session.RevokedAtUtc == null).FirstOrDefaultAsync(cancellationToken);

    public async Task<(UserSession Session, bool Created)> AddActiveOrGetExistingAsync(UserSession session, CancellationToken cancellationToken)
    {
        try
        {
            await _sessions.InsertOneAsync(session, cancellationToken: cancellationToken);
            return (session, true);
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            UserSession existing = await GetActiveByUserIdAndSessionTypeAsync(session.UserId, session.SessionType, cancellationToken) ?? throw new InvalidOperationException("Active session unique index was violated but no active session was found.");
            return (existing, false);
        }
    }

    public Task UpdateAsync(UserSession session, CancellationToken cancellationToken) => _sessions.ReplaceOneAsync(existing => existing.Id == session.Id, session, cancellationToken: cancellationToken);
}
