using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoDeviceActivationCodeRepository : IDeviceActivationCodeRepository
{
    private readonly IMongoCollection<DeviceActivationCode> _codes;

    public MongoDeviceActivationCodeRepository(IMongoDatabase database)
    {
        _codes = database.GetCollection<DeviceActivationCode>(MongoCollectionNames.DeviceActivationCodes);
    }

    public async Task<IReadOnlyList<DeviceActivationCode>> GetActiveByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await _codes.Find(code => code.UserId == userId && !code.IsUsed && !code.IsRevoked && code.ExpiresAtUtc > now).ToListAsync(cancellationToken);

    public async Task<DeviceActivationCode?> GetActiveCurrentByUserIdAsync(string userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await _codes.Find(code => code.UserId == userId && !code.IsUsed && !code.IsRevoked && code.ExpiresAtUtc > now)
            .SortByDescending(code => code.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<DeviceActivationCode?> GetByCodeAsync(string code, CancellationToken cancellationToken) =>
        await _codes.Find(activationCode => activationCode.Code == code).FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(DeviceActivationCode code, CancellationToken cancellationToken) =>
        await _codes.InsertOneAsync(code, cancellationToken: cancellationToken);

    public async Task UpdateAsync(DeviceActivationCode code, CancellationToken cancellationToken) =>
        await _codes.ReplaceOneAsync(existing => existing.Id == code.Id, code, cancellationToken: cancellationToken);
}
