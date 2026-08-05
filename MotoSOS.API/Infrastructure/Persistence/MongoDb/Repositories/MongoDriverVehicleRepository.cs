using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;

public sealed class MongoDriverVehicleRepository : IDriverVehicleRepository
{
    private readonly IMongoCollection<DriverVehicle> _vehicles;

    public MongoDriverVehicleRepository(IMongoDatabase database)
    {
        _vehicles = database.GetCollection<DriverVehicle>(MongoCollectionNames.DriverVehicles);
    }

    public async Task<IReadOnlyList<DriverVehicle>> GetActiveByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        return await _vehicles.Find(vehicle => vehicle.UserId == userId && vehicle.IsActive).ToListAsync(cancellationToken);
    }

    public async Task<DriverVehicle?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        return await _vehicles.Find(vehicle => vehicle.Id == id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CountActiveByUserIdAsync(string userId, CancellationToken cancellationToken)
    {
        long count = await _vehicles.CountDocumentsAsync(vehicle => vehicle.UserId == userId && vehicle.IsActive, cancellationToken: cancellationToken);
        return (int)count;
    }

    public async Task AddAsync(DriverVehicle vehicle, CancellationToken cancellationToken)
    {
        await _vehicles.InsertOneAsync(vehicle, cancellationToken: cancellationToken);
    }

    public async Task UpdateAsync(DriverVehicle vehicle, CancellationToken cancellationToken)
    {
        await _vehicles.ReplaceOneAsync(existing => existing.Id == vehicle.Id, vehicle, cancellationToken: cancellationToken);
    }
}
