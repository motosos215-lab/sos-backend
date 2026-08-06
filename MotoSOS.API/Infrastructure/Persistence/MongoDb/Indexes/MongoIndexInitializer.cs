using MongoDB.Bson;
using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.Onboarding.Domain;
using MotoSOS.API.Modules.Plans.Domain;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Indexes;

public sealed class MongoIndexInitializer
{
    private readonly IMongoDatabase _database;

    public MongoIndexInitializer(IMongoDatabase database)
    {
        _database = database;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        IMongoCollection<User> users = _database.GetCollection<User>(MongoCollectionNames.Users);
        await EnsureIndexAsync(users, "ux_users_email", new BsonDocument(nameof(User.Email), 1), unique: true, cancellationToken);

        IMongoCollection<RefreshToken> refreshTokens = _database.GetCollection<RefreshToken>(MongoCollectionNames.RefreshTokens);
        await EnsureIndexAsync(refreshTokens, "ux_refreshTokens_tokenHash", new BsonDocument(nameof(RefreshToken.TokenHash), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(refreshTokens, "ix_refreshTokens_userId", new BsonDocument(nameof(RefreshToken.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(
            refreshTokens,
            "ix_refreshTokens_userId_revokedAtUtc_expiresAtUtc",
            new BsonDocument
            {
                [nameof(RefreshToken.UserId)] = 1,
                [nameof(RefreshToken.RevokedAtUtc)] = 1,
                [nameof(RefreshToken.ExpiresAtUtc)] = 1
            },
            unique: false,
            cancellationToken);
        await EnsureIndexAsync(
            refreshTokens,
            "ix_refreshTokens_userId_expiresAtUtc",
            new BsonDocument
            {
                [nameof(RefreshToken.UserId)] = 1,
                [nameof(RefreshToken.ExpiresAtUtc)] = 1
            },
            unique: false,
            cancellationToken);

        IMongoCollection<DriverProfile> driverProfiles = _database.GetCollection<DriverProfile>(MongoCollectionNames.DriverProfiles);
        await EnsureIndexAsync(driverProfiles, "ux_driverProfiles_userId", new BsonDocument(nameof(DriverProfile.UserId), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(driverProfiles, "ix_driverProfiles_completionStatus", new BsonDocument(nameof(DriverProfile.CompletionStatus), 1), unique: false, cancellationToken);

        IMongoCollection<DriverVehicle> driverVehicles = _database.GetCollection<DriverVehicle>(MongoCollectionNames.DriverVehicles);
        await EnsureIndexAsync(driverVehicles, "ix_driverVehicles_userId", new BsonDocument(nameof(DriverVehicle.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(
            driverVehicles,
            "ix_driverVehicles_userId_isActive",
            new BsonDocument
            {
                [nameof(DriverVehicle.UserId)] = 1,
                [nameof(DriverVehicle.IsActive)] = 1
            },
            unique: false,
            cancellationToken);
        await EnsureIndexAsync(driverVehicles, "ix_driverVehicles_completionStatus", new BsonDocument(nameof(DriverVehicle.CompletionStatus), 1), unique: false, cancellationToken);

        IMongoCollection<EmergencyContact> emergencyContacts = _database.GetCollection<EmergencyContact>(MongoCollectionNames.EmergencyContacts);
        await EnsureIndexAsync(emergencyContacts, "ix_emergencyContacts_userId", new BsonDocument(nameof(EmergencyContact.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(
            emergencyContacts,
            "ix_emergencyContacts_userId_isActive",
            new BsonDocument
            {
                [nameof(EmergencyContact.UserId)] = 1,
                [nameof(EmergencyContact.IsActive)] = 1
            },
            unique: false,
            cancellationToken);
        await EnsureIndexAsync(emergencyContacts, "ix_emergencyContacts_invitationStatus", new BsonDocument(nameof(EmergencyContact.InvitationStatus), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyContacts, "ix_emergencyContacts_linkingCode", new BsonDocument(nameof(EmergencyContact.LinkingCode), 1), unique: false, cancellationToken);

        IMongoCollection<DeviceActivationCode> activationCodes = _database.GetCollection<DeviceActivationCode>(MongoCollectionNames.DeviceActivationCodes);
        await EnsureIndexAsync(activationCodes, "ix_deviceActivationCodes_userId", new BsonDocument(nameof(DeviceActivationCode.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(activationCodes, "ix_deviceActivationCodes_code", new BsonDocument(nameof(DeviceActivationCode.Code), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(
            activationCodes,
            "ix_deviceActivationCodes_userId_isUsed_isRevoked",
            new BsonDocument
            {
                [nameof(DeviceActivationCode.UserId)] = 1,
                [nameof(DeviceActivationCode.IsUsed)] = 1,
                [nameof(DeviceActivationCode.IsRevoked)] = 1
            },
            unique: false,
            cancellationToken);
        await EnsureIndexAsync(activationCodes, "ix_deviceActivationCodes_expiresAtUtc", new BsonDocument(nameof(DeviceActivationCode.ExpiresAtUtc), 1), unique: false, cancellationToken);

        IMongoCollection<UserDevice> userDevices = _database.GetCollection<UserDevice>(MongoCollectionNames.UserDevices);
        await EnsureIndexAsync(userDevices, "ix_userDevices_userId", new BsonDocument(nameof(UserDevice.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(
            userDevices,
            "ix_userDevices_userId_isActive",
            new BsonDocument
            {
                [nameof(UserDevice.UserId)] = 1,
                [nameof(UserDevice.IsActive)] = 1
            },
            unique: false,
            cancellationToken);
        await EnsureIndexAsync(
            userDevices,
            "ix_userDevices_userId_deviceType",
            new BsonDocument
            {
                [nameof(UserDevice.UserId)] = 1,
                [nameof(UserDevice.DeviceType)] = 1
            },
            unique: false,
            cancellationToken);
        await EnsureIndexAsync(userDevices, "ix_userDevices_parentDeviceId", new BsonDocument(nameof(UserDevice.ParentDeviceId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(userDevices, "ix_userDevices_linkStatus", new BsonDocument(nameof(UserDevice.LinkStatus), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(userDevices, "ix_userDevices_deviceIdentifierHash", new BsonDocument(nameof(UserDevice.DeviceIdentifierHash), 1), unique: false, cancellationToken);

        IMongoCollection<UserSubscription> userSubscriptions = _database.GetCollection<UserSubscription>(MongoCollectionNames.UserSubscriptions);
        await EnsureIndexAsync(userSubscriptions, "ix_userSubscriptions_userId", new BsonDocument(nameof(UserSubscription.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(
            userSubscriptions,
            "ix_userSubscriptions_userId_status",
            new BsonDocument
            {
                [nameof(UserSubscription.UserId)] = 1,
                [nameof(UserSubscription.Status)] = 1
            },
            unique: false,
            cancellationToken);
        await EnsureIndexAsync(userSubscriptions, "ix_userSubscriptions_planTier", new BsonDocument(nameof(UserSubscription.PlanTier), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(userSubscriptions, "ix_userSubscriptions_source", new BsonDocument(nameof(UserSubscription.Source), 1), unique: false, cancellationToken);

        IMongoCollection<OnboardingConfirmation> onboardingConfirmations = _database.GetCollection<OnboardingConfirmation>(MongoCollectionNames.OnboardingConfirmations);
        await EnsureIndexAsync(onboardingConfirmations, "ix_onboardingConfirmations_userId", new BsonDocument(nameof(OnboardingConfirmation.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(onboardingConfirmations, "ix_onboardingConfirmations_isOperational", new BsonDocument(nameof(OnboardingConfirmation.IsOperational), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(onboardingConfirmations, "ix_onboardingConfirmations_confirmedAtUtc", new BsonDocument(nameof(OnboardingConfirmation.ConfirmedAtUtc), 1), unique: false, cancellationToken);

        IMongoCollection<Trip> trips = _database.GetCollection<Trip>(MongoCollectionNames.Trips);
        await EnsureIndexAsync(trips, "ix_trips_userId", new BsonDocument(nameof(Trip.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(
            trips,
            "ix_trips_userId_status",
            new BsonDocument
            {
                [nameof(Trip.UserId)] = 1,
                [nameof(Trip.Status)] = 1
            },
            unique: false,
            cancellationToken);
        await EnsureIndexAsync(trips, "ix_trips_vehicleId", new BsonDocument(nameof(Trip.VehicleId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(trips, "ix_trips_mobileDeviceId", new BsonDocument(nameof(Trip.MobileDeviceId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(trips, "ix_trips_startedAtUtc", new BsonDocument(nameof(Trip.StartedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(trips, "ix_trips_finishedAtUtc", new BsonDocument(nameof(Trip.FinishedAtUtc), 1), unique: false, cancellationToken);

        IMongoCollection<OfflineIngestionRecord> offlineIngestionRecords = _database.GetCollection<OfflineIngestionRecord>(MongoCollectionNames.OfflineIngestionRecords);
        await EnsureIndexAsync(offlineIngestionRecords, "ix_offlineIngestionRecords_userId", new BsonDocument(nameof(OfflineIngestionRecord.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(offlineIngestionRecords, "ix_offlineIngestionRecords_mobileDeviceId", new BsonDocument(nameof(OfflineIngestionRecord.MobileDeviceId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(offlineIngestionRecords, "ix_offlineIngestionRecords_tripId", new BsonDocument(nameof(OfflineIngestionRecord.TripId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(offlineIngestionRecords, "ix_offlineIngestionRecords_batchId", new BsonDocument(nameof(OfflineIngestionRecord.BatchId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(offlineIngestionRecords, "ix_offlineIngestionRecords_clientEventId", new BsonDocument(nameof(OfflineIngestionRecord.ClientEventId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(offlineIngestionRecords, "ix_offlineIngestionRecords_type", new BsonDocument(nameof(OfflineIngestionRecord.Type), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(offlineIngestionRecords, "ux_offlineIngestionRecords_idempotencyKey", new BsonDocument(nameof(OfflineIngestionRecord.IdempotencyKey), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(offlineIngestionRecords, "ix_offlineIngestionRecords_ackId", new BsonDocument(nameof(OfflineIngestionRecord.AckId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(offlineIngestionRecords, "ix_offlineIngestionRecords_processingStatus", new BsonDocument(nameof(OfflineIngestionRecord.ProcessingStatus), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(offlineIngestionRecords, "ix_offlineIngestionRecords_receivedAtUtc", new BsonDocument(nameof(OfflineIngestionRecord.ReceivedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(offlineIngestionRecords, "ix_offlineIngestionRecords_occurredAtUtc", new BsonDocument(nameof(OfflineIngestionRecord.OccurredAtUtc), 1), unique: false, cancellationToken);
    }

    private static async Task EnsureIndexAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        string name,
        BsonDocument key,
        bool unique,
        CancellationToken cancellationToken)
    {
        if (await HasEquivalentIndexAsync(collection, key, unique, cancellationToken))
        {
            return;
        }

        var index = new CreateIndexModel<TDocument>(
            new BsonDocumentIndexKeysDefinition<TDocument>(key),
            new CreateIndexOptions { Name = name, Unique = unique });

        try
        {
            await collection.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
        }
        catch (MongoCommandException exception) when (IsIndexConflict(exception))
        {
            if (await HasEquivalentIndexAsync(collection, key, unique, cancellationToken))
            {
                return;
            }

            throw;
        }
    }

    private static async Task<bool> HasEquivalentIndexAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        BsonDocument key,
        bool unique,
        CancellationToken cancellationToken)
    {
        using IAsyncCursor<BsonDocument> cursor = await collection.Indexes.ListAsync(cancellationToken: cancellationToken);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (BsonDocument existingIndex in cursor.Current)
            {
                if (IsEquivalentIndex(existingIndex, key, unique))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsEquivalentIndex(BsonDocument existingIndex, BsonDocument key, bool unique)
    {
        if (!existingIndex.TryGetValue("key", out BsonValue existingKey) || !existingKey.IsBsonDocument)
        {
            return false;
        }

        bool existingUnique = existingIndex.TryGetValue("unique", out BsonValue uniqueValue) && uniqueValue.ToBoolean();

        return existingKey.AsBsonDocument.Equals(key) && existingUnique == unique;
    }

    private static bool IsIndexConflict(MongoCommandException exception)
    {
        return exception.Code is 85 or 86 ||
            exception.Message.Contains("Index already exists with a different name", StringComparison.OrdinalIgnoreCase);
    }
}
