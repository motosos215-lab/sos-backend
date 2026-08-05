using System.Net;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using Moq;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Indexes;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace UnitTest.Infrastructure.Persistence.MongoDb.Indexes;

public sealed class MongoIndexInitializerTests
{
    [Fact]
    public async Task EnsureIndexesAsyncSkipsEquivalentIndexesWithDifferentNames()
    {
        TestMongoIndexes indexes = CreateIndexes(
            UserEmailIndex("legacy_email_unique"),
            RefreshTokenHashIndex("legacy_token_hash_unique"),
            RefreshTokenUserIdIndex("legacy_user_id"),
            RefreshTokenUserRevokedExpirationIndex("legacy_user_revoked_expiration"),
            RefreshTokenUserExpirationIndex("legacy_user_expiration"),
            DriverProfileUserIdIndex("legacy_driver_profile_user_id"),
            DriverProfileCompletionStatusIndex("legacy_driver_profile_completion"),
            DriverVehicleUserIdIndex("legacy_driver_vehicle_user_id"),
            DriverVehicleUserIdIsActiveIndex("legacy_driver_vehicle_user_active"),
            DriverVehicleCompletionStatusIndex("legacy_driver_vehicle_completion"),
            EmergencyContactUserIdIndex("legacy_emergency_contact_user_id"),
            EmergencyContactUserIdIsActiveIndex("legacy_emergency_contact_user_active"),
            EmergencyContactInvitationStatusIndex("legacy_emergency_contact_status"),
            EmergencyContactLinkingCodeIndex("legacy_emergency_contact_code"));
        var initializer = new MongoIndexInitializer(indexes.Database.Object);

        await initializer.EnsureIndexesAsync(CancellationToken.None);

        indexes.UserIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<User>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        indexes.RefreshTokenIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<RefreshToken>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        indexes.DriverProfileIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<DriverProfile>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        indexes.DriverVehicleIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<DriverVehicle>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        indexes.EmergencyContactIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<EmergencyContact>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnsureIndexesAsyncCreatesMissingIndexes()
    {
        TestMongoIndexes indexes = CreateIndexes(
            UserEmailIndex("legacy_email_unique"),
            RefreshTokenUserIdIndex("legacy_user_id"),
            RefreshTokenUserRevokedExpirationIndex("legacy_user_revoked_expiration"),
            RefreshTokenUserExpirationIndex("legacy_user_expiration"),
            DriverProfileCompletionStatusIndex("legacy_driver_profile_completion"),
            DriverVehicleUserIdIsActiveIndex("legacy_driver_vehicle_user_active"),
            DriverVehicleCompletionStatusIndex("legacy_driver_vehicle_completion"),
            EmergencyContactUserIdIsActiveIndex("legacy_emergency_contact_user_active"),
            EmergencyContactInvitationStatusIndex("legacy_emergency_contact_status"),
            EmergencyContactLinkingCodeIndex("legacy_emergency_contact_code"));
        var initializer = new MongoIndexInitializer(indexes.Database.Object);

        await initializer.EnsureIndexesAsync(CancellationToken.None);

        indexes.RefreshTokenIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(
                It.Is<CreateIndexModel<RefreshToken>>(model => model.Options.Name == "ux_refreshTokens_tokenHash" && model.Options.Unique == true),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        indexes.DriverProfileIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(
                It.Is<CreateIndexModel<DriverProfile>>(model => model.Options.Name == "ux_driverProfiles_userId" && model.Options.Unique == true),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        indexes.DriverVehicleIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(
                It.Is<CreateIndexModel<DriverVehicle>>(model => model.Options.Name == "ix_driverVehicles_userId" && model.Options.Unique == false),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        indexes.EmergencyContactIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(
                It.Is<CreateIndexModel<EmergencyContact>>(model => model.Options.Name == "ix_emergencyContacts_userId" && model.Options.Unique == false),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnsureIndexesAsyncToleratesIndexNameConflictWhenEquivalentIndexExists()
    {
        TestMongoIndexes indexes = CreateIndexes(
            RefreshTokenHashIndex("legacy_token_hash_unique"),
            RefreshTokenUserIdIndex("legacy_user_id"),
            RefreshTokenUserRevokedExpirationIndex("legacy_user_revoked_expiration"),
            RefreshTokenUserExpirationIndex("legacy_user_expiration"),
            DriverProfileUserIdIndex("legacy_driver_profile_user_id"),
            DriverProfileCompletionStatusIndex("legacy_driver_profile_completion"),
            DriverVehicleUserIdIndex("legacy_driver_vehicle_user_id"),
            DriverVehicleUserIdIsActiveIndex("legacy_driver_vehicle_user_active"),
            DriverVehicleCompletionStatusIndex("legacy_driver_vehicle_completion"),
            EmergencyContactUserIdIndex("legacy_emergency_contact_user_id"),
            EmergencyContactUserIdIsActiveIndex("legacy_emergency_contact_user_active"),
            EmergencyContactInvitationStatusIndex("legacy_emergency_contact_status"),
            EmergencyContactLinkingCodeIndex("legacy_emergency_contact_code"));
        indexes.UserIndexes
            .SetupSequence(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BsonDocumentCursor([]))
            .ReturnsAsync(new BsonDocumentCursor([UserEmailIndex("email_1")]));
        indexes.UserIndexes
            .Setup(indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<User>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateIndexNameConflictException());
        var initializer = new MongoIndexInitializer(indexes.Database.Object);

        Func<Task> act = () => initializer.EnsureIndexesAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureIndexesAsyncDoesNotHideRealMongoErrors()
    {
        TestMongoIndexes indexes = CreateIndexes();
        InvalidOperationException expectedException = new("MongoDB failure");
        indexes.UserIndexes
            .Setup(indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<User>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);
        var initializer = new MongoIndexInitializer(indexes.Database.Object);

        Func<Task> act = () => initializer.EnsureIndexesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("MongoDB failure");
    }

    private static TestMongoIndexes CreateIndexes(params BsonDocument[] existingIndexes)
    {
        var database = new Mock<IMongoDatabase>();
        var users = new Mock<IMongoCollection<User>>();
        var refreshTokens = new Mock<IMongoCollection<RefreshToken>>();
        var driverProfiles = new Mock<IMongoCollection<DriverProfile>>();
        var driverVehicles = new Mock<IMongoCollection<DriverVehicle>>();
        var emergencyContacts = new Mock<IMongoCollection<EmergencyContact>>();
        var userIndexes = new Mock<IMongoIndexManager<User>>();
        var refreshTokenIndexes = new Mock<IMongoIndexManager<RefreshToken>>();
        var driverProfileIndexes = new Mock<IMongoIndexManager<DriverProfile>>();
        var driverVehicleIndexes = new Mock<IMongoIndexManager<DriverVehicle>>();
        var emergencyContactIndexes = new Mock<IMongoIndexManager<EmergencyContact>>();

        users.SetupGet(collection => collection.Indexes).Returns(userIndexes.Object);
        refreshTokens.SetupGet(collection => collection.Indexes).Returns(refreshTokenIndexes.Object);
        driverProfiles.SetupGet(collection => collection.Indexes).Returns(driverProfileIndexes.Object);
        driverVehicles.SetupGet(collection => collection.Indexes).Returns(driverVehicleIndexes.Object);
        emergencyContacts.SetupGet(collection => collection.Indexes).Returns(emergencyContactIndexes.Object);
        database
            .Setup(db => db.GetCollection<User>(MongoCollectionNames.Users, It.IsAny<MongoCollectionSettings>()))
            .Returns(users.Object);
        database
            .Setup(db => db.GetCollection<RefreshToken>(MongoCollectionNames.RefreshTokens, It.IsAny<MongoCollectionSettings>()))
            .Returns(refreshTokens.Object);
        database
            .Setup(db => db.GetCollection<DriverProfile>(MongoCollectionNames.DriverProfiles, It.IsAny<MongoCollectionSettings>()))
            .Returns(driverProfiles.Object);
        database
            .Setup(db => db.GetCollection<DriverVehicle>(MongoCollectionNames.DriverVehicles, It.IsAny<MongoCollectionSettings>()))
            .Returns(driverVehicles.Object);
        database
            .Setup(db => db.GetCollection<EmergencyContact>(MongoCollectionNames.EmergencyContacts, It.IsAny<MongoCollectionSettings>()))
            .Returns(emergencyContacts.Object);

        userIndexes
            .Setup(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BsonDocumentCursor(existingIndexes.Where(index => index.GetValue("collection", string.Empty) == MongoCollectionNames.Users)));
        refreshTokenIndexes
            .Setup(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BsonDocumentCursor(existingIndexes.Where(index => index.GetValue("collection", string.Empty) == MongoCollectionNames.RefreshTokens)));
        driverProfileIndexes
            .Setup(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BsonDocumentCursor(existingIndexes.Where(index => index.GetValue("collection", string.Empty) == MongoCollectionNames.DriverProfiles)));
        driverVehicleIndexes
            .Setup(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BsonDocumentCursor(existingIndexes.Where(index => index.GetValue("collection", string.Empty) == MongoCollectionNames.DriverVehicles)));
        emergencyContactIndexes
            .Setup(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BsonDocumentCursor(existingIndexes.Where(index => index.GetValue("collection", string.Empty) == MongoCollectionNames.EmergencyContacts)));

        return new TestMongoIndexes(database, userIndexes, refreshTokenIndexes, driverProfileIndexes, driverVehicleIndexes, emergencyContactIndexes);
    }

    private static MongoCommandException CreateIndexNameConflictException()
    {
        var result = new BsonDocument
        {
            ["ok"] = 0,
            ["code"] = 86,
            ["errmsg"] = "Index already exists with a different name: ux_users_email"
        };
        var connectionId = new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017)));

        return new MongoCommandException(
            connectionId,
            "Command createIndexes failed: Index already exists with a different name: ux_users_email.",
            new BsonDocument("createIndexes", MongoCollectionNames.Users),
            result);
    }

    private static BsonDocument UserEmailIndex(string name) => Index(MongoCollectionNames.Users, name, new BsonDocument(nameof(User.Email), 1), unique: true);

    private static BsonDocument RefreshTokenHashIndex(string name) => Index(MongoCollectionNames.RefreshTokens, name, new BsonDocument(nameof(RefreshToken.TokenHash), 1), unique: true);

    private static BsonDocument RefreshTokenUserIdIndex(string name) => Index(MongoCollectionNames.RefreshTokens, name, new BsonDocument(nameof(RefreshToken.UserId), 1), unique: false);

    private static BsonDocument RefreshTokenUserRevokedExpirationIndex(string name) => Index(
        MongoCollectionNames.RefreshTokens,
        name,
        new BsonDocument
        {
            [nameof(RefreshToken.UserId)] = 1,
            [nameof(RefreshToken.RevokedAtUtc)] = 1,
            [nameof(RefreshToken.ExpiresAtUtc)] = 1
        },
        unique: false);

    private static BsonDocument RefreshTokenUserExpirationIndex(string name) => Index(
        MongoCollectionNames.RefreshTokens,
        name,
        new BsonDocument
        {
            [nameof(RefreshToken.UserId)] = 1,
            [nameof(RefreshToken.ExpiresAtUtc)] = 1
        },
        unique: false);

    private static BsonDocument DriverProfileUserIdIndex(string name) => Index(MongoCollectionNames.DriverProfiles, name, new BsonDocument(nameof(DriverProfile.UserId), 1), unique: true);

    private static BsonDocument DriverProfileCompletionStatusIndex(string name) => Index(MongoCollectionNames.DriverProfiles, name, new BsonDocument(nameof(DriverProfile.CompletionStatus), 1), unique: false);

    private static BsonDocument DriverVehicleUserIdIndex(string name) => Index(MongoCollectionNames.DriverVehicles, name, new BsonDocument(nameof(DriverVehicle.UserId), 1), unique: false);

    private static BsonDocument DriverVehicleUserIdIsActiveIndex(string name) => Index(
        MongoCollectionNames.DriverVehicles,
        name,
        new BsonDocument
        {
            [nameof(DriverVehicle.UserId)] = 1,
            [nameof(DriverVehicle.IsActive)] = 1
        },
        unique: false);

    private static BsonDocument DriverVehicleCompletionStatusIndex(string name) => Index(MongoCollectionNames.DriverVehicles, name, new BsonDocument(nameof(DriverVehicle.CompletionStatus), 1), unique: false);

    private static BsonDocument EmergencyContactUserIdIndex(string name) => Index(MongoCollectionNames.EmergencyContacts, name, new BsonDocument(nameof(EmergencyContact.UserId), 1), unique: false);

    private static BsonDocument EmergencyContactUserIdIsActiveIndex(string name) => Index(
        MongoCollectionNames.EmergencyContacts,
        name,
        new BsonDocument
        {
            [nameof(EmergencyContact.UserId)] = 1,
            [nameof(EmergencyContact.IsActive)] = 1
        },
        unique: false);

    private static BsonDocument EmergencyContactInvitationStatusIndex(string name) => Index(MongoCollectionNames.EmergencyContacts, name, new BsonDocument(nameof(EmergencyContact.InvitationStatus), 1), unique: false);

    private static BsonDocument EmergencyContactLinkingCodeIndex(string name) => Index(MongoCollectionNames.EmergencyContacts, name, new BsonDocument(nameof(EmergencyContact.LinkingCode), 1), unique: false);

    private static BsonDocument Index(string collection, string name, BsonDocument key, bool unique)
    {
        var index = new BsonDocument
        {
            ["collection"] = collection,
            ["name"] = name,
            ["key"] = key
        };

        if (unique)
        {
            index["unique"] = true;
        }

        return index;
    }

    private sealed record TestMongoIndexes(
        Mock<IMongoDatabase> Database,
        Mock<IMongoIndexManager<User>> UserIndexes,
        Mock<IMongoIndexManager<RefreshToken>> RefreshTokenIndexes,
        Mock<IMongoIndexManager<DriverProfile>> DriverProfileIndexes,
        Mock<IMongoIndexManager<DriverVehicle>> DriverVehicleIndexes,
        Mock<IMongoIndexManager<EmergencyContact>> EmergencyContactIndexes);

    private sealed class BsonDocumentCursor : IAsyncCursor<BsonDocument>
    {
        private readonly IReadOnlyList<BsonDocument> _documents;
        private bool _moved;

        public BsonDocumentCursor(IEnumerable<BsonDocument> documents)
        {
            _documents = documents.ToArray();
        }

        public IEnumerable<BsonDocument> Current { get; private set; } = [];

        public bool MoveNext(CancellationToken cancellationToken = default)
        {
            if (_moved)
            {
                Current = [];
                return false;
            }

            _moved = true;
            Current = _documents;
            return true;
        }

        public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(MoveNext(cancellationToken));

        public void Dispose()
        {
        }
    }
}
