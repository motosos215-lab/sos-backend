using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoUserDeviceRepository : IUserDeviceRepository
{
    private readonly IMongoCollection<UserDevice> _devices;

    public MongoUserDeviceRepository(IMongoDatabase database)
    {
        _devices = database.GetCollection<UserDevice>(MongoCollectionNames.UserDevices);
    }

    public async Task<IReadOnlyList<UserDevice>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken) =>
        await _devices.Find(device => device.UserId == userId && device.IsActive).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserDevice>> GetActiveByParentDeviceIdAsync(string parentDeviceId, CancellationToken cancellationToken) =>
        await _devices.Find(device => device.ParentDeviceId == parentDeviceId && device.IsActive).ToListAsync(cancellationToken);

    public async Task<UserDevice?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
        await _devices.Find(device => device.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<UserDevice?> GetByDeviceIdentifierHashAsync(string userId, string hash, DeviceType deviceType, CancellationToken cancellationToken) =>
        await _devices.Find(device => device.UserId == userId && device.DeviceIdentifierHash == hash && device.DeviceType == deviceType).FirstOrDefaultAsync(cancellationToken);

    public async Task<int> CountActiveLinkedByUserIdAndTypeAsync(string userId, DeviceType deviceType, CancellationToken cancellationToken)
    {
        long count = await _devices.CountDocumentsAsync(device => device.UserId == userId && device.DeviceType == deviceType && device.IsActive && device.LinkStatus == DeviceLinkStatus.Linked, cancellationToken: cancellationToken);
        return (int)count;
    }

    public async Task<bool> HasActiveLinkedMobileAppAsync(string userId, CancellationToken cancellationToken) =>
        await _devices.Find(device => device.UserId == userId && device.DeviceType == DeviceType.MobileApp && device.IsActive && device.LinkStatus == DeviceLinkStatus.Linked).AnyAsync(cancellationToken);

    public async Task AddAsync(UserDevice device, CancellationToken cancellationToken) =>
        await _devices.InsertOneAsync(device, cancellationToken: cancellationToken);

    public async Task UpdateAsync(UserDevice device, CancellationToken cancellationToken) =>
        await _devices.ReplaceOneAsync(existing => existing.Id == device.Id, device, cancellationToken: cancellationToken);
}
