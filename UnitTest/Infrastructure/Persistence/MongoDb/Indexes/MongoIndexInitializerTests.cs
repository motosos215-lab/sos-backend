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
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.Onboarding.Domain;
using MotoSOS.API.Modules.Plans.Domain;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.Trips.Domain;
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
            EmergencyContactLinkingCodeIndex("legacy_emergency_contact_code"),
            DeviceActivationCodeUserIdIndex("legacy_device_code_user_id"),
            DeviceActivationCodeCodeIndex("legacy_device_code_code"),
            DeviceActivationCodeUserUsedRevokedIndex("legacy_device_code_user_used_revoked"),
            DeviceActivationCodeExpiresAtIndex("legacy_device_code_expires"),
            UserDeviceUserIdIndex("legacy_user_device_user_id"),
            UserDeviceUserIdIsActiveIndex("legacy_user_device_user_active"),
            UserDeviceUserIdDeviceTypeIndex("legacy_user_device_user_type"),
            UserDeviceParentDeviceIdIndex("legacy_user_device_parent"),
            UserDeviceLinkStatusIndex("legacy_user_device_link_status"),
            UserDeviceIdentifierHashIndex("legacy_user_device_identifier"),
            UserSubscriptionUserIdIndex("legacy_user_subscription_user_id"),
            UserSubscriptionUserIdStatusIndex("legacy_user_subscription_user_status"),
            UserSubscriptionPlanTierIndex("legacy_user_subscription_plan"),
            UserSubscriptionSourceIndex("legacy_user_subscription_source"),
            OnboardingConfirmationUserIdIndex("legacy_onboarding_confirmation_user"),
            OnboardingConfirmationIsOperationalIndex("legacy_onboarding_confirmation_operational"),
            OnboardingConfirmationConfirmedAtIndex("legacy_onboarding_confirmation_confirmed"),
            TripUserIdIndex("legacy_trip_user"),
            TripUserIdStatusIndex("legacy_trip_user_status"),
            TripVehicleIdIndex("legacy_trip_vehicle"),
            TripMobileDeviceIdIndex("legacy_trip_mobile"),
            TripStartedAtIndex("legacy_trip_started"),
            TripFinishedAtIndex("legacy_trip_finished"),
            OfflineIngestionUserIdIndex("legacy_offline_user"),
            OfflineIngestionMobileDeviceIdIndex("legacy_offline_mobile"),
            OfflineIngestionTripIdIndex("legacy_offline_trip"),
            OfflineIngestionBatchIdIndex("legacy_offline_batch"),
            OfflineIngestionClientEventIdIndex("legacy_offline_client_event"),
            OfflineIngestionTypeIndex("legacy_offline_type"),
            OfflineIngestionIdempotencyKeyIndex("legacy_offline_idempotency"),
            OfflineIngestionAckIdIndex("legacy_offline_ack"),
            OfflineIngestionProcessingStatusIndex("legacy_offline_status"),
            OfflineIngestionReceivedAtIndex("legacy_offline_received"),
            OfflineIngestionOccurredAtIndex("legacy_offline_occurred"),
            IncidentUserIdIndex("legacy_incident_user"),
            IncidentTripIdIndex("legacy_incident_trip"),
            IncidentUserIdStatusIndex("legacy_incident_user_status"),
            IncidentClientIncidentIdIndex("legacy_incident_client"),
            IncidentIdempotencyKeyIndex("legacy_incident_idempotency"),
            IncidentOccurredAtIndex("legacy_incident_occurred"),
            IncidentCreatedAtIndex("legacy_incident_created"),
            IncidentClosedAtIndex("legacy_incident_closed"),
            AlertDispatchUserIdIndex("legacy_alert_user"),
            AlertDispatchIncidentIdIndex("legacy_alert_incident"),
            AlertDispatchUserIdStatusIndex("legacy_alert_user_status"),
            AlertDispatchClientAlertRequestIdIndex("legacy_alert_client"),
            AlertDispatchIdempotencyKeyIndex("legacy_alert_idempotency"),
            AlertDispatchRequestedAtIndex("legacy_alert_requested"),
            AlertDispatchCreatedAtIndex("legacy_alert_created"),
            AlertDispatchCancelledAtIndex("legacy_alert_cancelled"),
            AlertDispatchCompletedAtIndex("legacy_alert_completed"),
            NotificationUserIdIndex("legacy_notification_user"),
            NotificationAlertDispatchIdIndex("legacy_notification_alert"),
            NotificationIncidentIdIndex("legacy_notification_incident"),
            NotificationEmergencyContactIdIndex("legacy_notification_contact"),
            NotificationUserIdStatusIndex("legacy_notification_user_status"),
            NotificationChannelIndex("legacy_notification_channel"),
            NotificationIdempotencyKeyIndex("legacy_notification_idempotency"),
            NotificationPreparedAtIndex("legacy_notification_prepared"),
            NotificationSimulatedSentAtIndex("legacy_notification_sent"),
            NotificationFailedAtIndex("legacy_notification_failed"),
            NotificationCancelledAtIndex("legacy_notification_cancelled"),
            NotificationCreatedAtIndex("legacy_notification_created"),
            AlertAcknowledgementUserIdIndex("legacy_ack_user"),
            AlertAcknowledgementMonitorUserIdIndex("legacy_ack_monitor"),
            AlertAcknowledgementEmergencyContactIdIndex("legacy_ack_contact"),
            AlertAcknowledgementAlertDispatchIdIndex("legacy_ack_alert"),
            AlertAcknowledgementNotificationDeliveryAttemptIdIndex("legacy_ack_attempt"),
            AlertAcknowledgementIncidentIdIndex("legacy_ack_incident"),
            AlertAcknowledgementTripIdIndex("legacy_ack_trip"),
            AlertAcknowledgementStatusIndex("legacy_ack_status"),
            AlertAcknowledgementMonitorUserIdStatusIndex("legacy_ack_monitor_status"),
            AlertAcknowledgementUserIdStatusIndex("legacy_ack_user_status"),
            AlertAcknowledgementIdempotencyKeyIndex("legacy_ack_idempotency"),
            AlertAcknowledgementCreatedAtIndex("legacy_ack_created"),
            AlertAcknowledgementViewedAtIndex("legacy_ack_viewed"),
            AlertAcknowledgementAcknowledgedAtIndex("legacy_ack_acknowledged"),
            AlertAcknowledgementDeclinedAtIndex("legacy_ack_declined"));
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
        indexes.DeviceActivationCodeIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<DeviceActivationCode>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        indexes.UserDeviceIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<UserDevice>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        indexes.UserSubscriptionIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<UserSubscription>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        indexes.OnboardingConfirmationIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<OnboardingConfirmation>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        indexes.TripIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<Trip>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        indexes.OfflineIngestionIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<OfflineIngestionRecord>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        indexes.IncidentIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<Incident>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        indexes.AlertDispatchIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<AlertDispatchRequest>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        indexes.NotificationIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<NotificationDeliveryAttempt>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()),
            Times.Never);
        indexes.AlertAcknowledgementIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(It.IsAny<CreateIndexModel<AlertAcknowledgement>>(), It.IsAny<CreateOneIndexOptions>(), It.IsAny<CancellationToken>()),
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
        indexes.TripIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(
                It.Is<CreateIndexModel<Trip>>(model => model.Options.Name == "ix_trips_userId" && model.Options.Unique == false),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        indexes.OfflineIngestionIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(
                It.Is<CreateIndexModel<OfflineIngestionRecord>>(model => model.Options.Name == "ix_offlineIngestionRecords_userId" && model.Options.Unique == false),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        indexes.IncidentIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(
                It.Is<CreateIndexModel<Incident>>(model => model.Options.Name == "ix_incidents_userId" && model.Options.Unique == false),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        indexes.AlertDispatchIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(
                It.Is<CreateIndexModel<AlertDispatchRequest>>(model => model.Options.Name == "ux_alertDispatchRequests_idempotencyKey" && model.Options.Unique == true),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        indexes.NotificationIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(
                It.Is<CreateIndexModel<NotificationDeliveryAttempt>>(model => model.Options.Name == "ux_notificationDeliveryAttempts_idempotencyKey" && model.Options.Unique == true),
                It.IsAny<CreateOneIndexOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        indexes.AlertAcknowledgementIndexes.Verify(
            indexManager => indexManager.CreateOneAsync(
                It.Is<CreateIndexModel<AlertAcknowledgement>>(model => model.Options.Name == "ux_alertAcknowledgements_idempotencyKey" && model.Options.Unique == true),
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
        var deviceActivationCodes = new Mock<IMongoCollection<DeviceActivationCode>>();
        var userDevices = new Mock<IMongoCollection<UserDevice>>();
        var userSubscriptions = new Mock<IMongoCollection<UserSubscription>>();
        var onboardingConfirmations = new Mock<IMongoCollection<OnboardingConfirmation>>();
        var trips = new Mock<IMongoCollection<Trip>>();
        var offlineIngestionRecords = new Mock<IMongoCollection<OfflineIngestionRecord>>();
        var incidents = new Mock<IMongoCollection<Incident>>();
        var alertDispatchRequests = new Mock<IMongoCollection<AlertDispatchRequest>>();
        var notificationDeliveryAttempts = new Mock<IMongoCollection<NotificationDeliveryAttempt>>();
        var alertAcknowledgements = new Mock<IMongoCollection<AlertAcknowledgement>>();
        var userIndexes = new Mock<IMongoIndexManager<User>>();
        var refreshTokenIndexes = new Mock<IMongoIndexManager<RefreshToken>>();
        var driverProfileIndexes = new Mock<IMongoIndexManager<DriverProfile>>();
        var driverVehicleIndexes = new Mock<IMongoIndexManager<DriverVehicle>>();
        var emergencyContactIndexes = new Mock<IMongoIndexManager<EmergencyContact>>();
        var deviceActivationCodeIndexes = new Mock<IMongoIndexManager<DeviceActivationCode>>();
        var userDeviceIndexes = new Mock<IMongoIndexManager<UserDevice>>();
        var userSubscriptionIndexes = new Mock<IMongoIndexManager<UserSubscription>>();
        var onboardingConfirmationIndexes = new Mock<IMongoIndexManager<OnboardingConfirmation>>();
        var tripIndexes = new Mock<IMongoIndexManager<Trip>>();
        var offlineIngestionIndexes = new Mock<IMongoIndexManager<OfflineIngestionRecord>>();
        var incidentIndexes = new Mock<IMongoIndexManager<Incident>>();
        var alertDispatchIndexes = new Mock<IMongoIndexManager<AlertDispatchRequest>>();
        var notificationIndexes = new Mock<IMongoIndexManager<NotificationDeliveryAttempt>>();
        var alertAcknowledgementIndexes = new Mock<IMongoIndexManager<AlertAcknowledgement>>();

        users.SetupGet(collection => collection.Indexes).Returns(userIndexes.Object);
        refreshTokens.SetupGet(collection => collection.Indexes).Returns(refreshTokenIndexes.Object);
        driverProfiles.SetupGet(collection => collection.Indexes).Returns(driverProfileIndexes.Object);
        driverVehicles.SetupGet(collection => collection.Indexes).Returns(driverVehicleIndexes.Object);
        emergencyContacts.SetupGet(collection => collection.Indexes).Returns(emergencyContactIndexes.Object);
        deviceActivationCodes.SetupGet(collection => collection.Indexes).Returns(deviceActivationCodeIndexes.Object);
        userDevices.SetupGet(collection => collection.Indexes).Returns(userDeviceIndexes.Object);
        userSubscriptions.SetupGet(collection => collection.Indexes).Returns(userSubscriptionIndexes.Object);
        onboardingConfirmations.SetupGet(collection => collection.Indexes).Returns(onboardingConfirmationIndexes.Object);
        trips.SetupGet(collection => collection.Indexes).Returns(tripIndexes.Object);
        offlineIngestionRecords.SetupGet(collection => collection.Indexes).Returns(offlineIngestionIndexes.Object);
        incidents.SetupGet(collection => collection.Indexes).Returns(incidentIndexes.Object);
        alertDispatchRequests.SetupGet(collection => collection.Indexes).Returns(alertDispatchIndexes.Object);
        notificationDeliveryAttempts.SetupGet(collection => collection.Indexes).Returns(notificationIndexes.Object);
        alertAcknowledgements.SetupGet(collection => collection.Indexes).Returns(alertAcknowledgementIndexes.Object);
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
        database
            .Setup(db => db.GetCollection<DeviceActivationCode>(MongoCollectionNames.DeviceActivationCodes, It.IsAny<MongoCollectionSettings>()))
            .Returns(deviceActivationCodes.Object);
        database
            .Setup(db => db.GetCollection<UserDevice>(MongoCollectionNames.UserDevices, It.IsAny<MongoCollectionSettings>()))
            .Returns(userDevices.Object);
        database
            .Setup(db => db.GetCollection<UserSubscription>(MongoCollectionNames.UserSubscriptions, It.IsAny<MongoCollectionSettings>()))
            .Returns(userSubscriptions.Object);
        database
            .Setup(db => db.GetCollection<OnboardingConfirmation>(MongoCollectionNames.OnboardingConfirmations, It.IsAny<MongoCollectionSettings>()))
            .Returns(onboardingConfirmations.Object);
        database
            .Setup(db => db.GetCollection<Trip>(MongoCollectionNames.Trips, It.IsAny<MongoCollectionSettings>()))
            .Returns(trips.Object);
        database
            .Setup(db => db.GetCollection<OfflineIngestionRecord>(MongoCollectionNames.OfflineIngestionRecords, It.IsAny<MongoCollectionSettings>()))
            .Returns(offlineIngestionRecords.Object);
        database
            .Setup(db => db.GetCollection<Incident>(MongoCollectionNames.Incidents, It.IsAny<MongoCollectionSettings>()))
            .Returns(incidents.Object);
        database
            .Setup(db => db.GetCollection<AlertDispatchRequest>(MongoCollectionNames.AlertDispatchRequests, It.IsAny<MongoCollectionSettings>()))
            .Returns(alertDispatchRequests.Object);
        database
            .Setup(db => db.GetCollection<NotificationDeliveryAttempt>(MongoCollectionNames.NotificationDeliveryAttempts, It.IsAny<MongoCollectionSettings>()))
            .Returns(notificationDeliveryAttempts.Object);
        database
            .Setup(db => db.GetCollection<AlertAcknowledgement>(MongoCollectionNames.AlertAcknowledgements, It.IsAny<MongoCollectionSettings>()))
            .Returns(alertAcknowledgements.Object);

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
        deviceActivationCodeIndexes
            .Setup(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BsonDocumentCursor(existingIndexes.Where(index => index.GetValue("collection", string.Empty) == MongoCollectionNames.DeviceActivationCodes)));
        userDeviceIndexes
            .Setup(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BsonDocumentCursor(existingIndexes.Where(index => index.GetValue("collection", string.Empty) == MongoCollectionNames.UserDevices)));
        userSubscriptionIndexes
            .Setup(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BsonDocumentCursor(existingIndexes.Where(index => index.GetValue("collection", string.Empty) == MongoCollectionNames.UserSubscriptions)));
        onboardingConfirmationIndexes
            .Setup(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BsonDocumentCursor(existingIndexes.Where(index => index.GetValue("collection", string.Empty) == MongoCollectionNames.OnboardingConfirmations)));
        tripIndexes
            .Setup(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BsonDocumentCursor(existingIndexes.Where(index => index.GetValue("collection", string.Empty) == MongoCollectionNames.Trips)));
        offlineIngestionIndexes
            .Setup(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BsonDocumentCursor(existingIndexes.Where(index => index.GetValue("collection", string.Empty) == MongoCollectionNames.OfflineIngestionRecords)));
        incidentIndexes
            .Setup(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BsonDocumentCursor(existingIndexes.Where(index => index.GetValue("collection", string.Empty) == MongoCollectionNames.Incidents)));
        alertDispatchIndexes
            .Setup(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BsonDocumentCursor(existingIndexes.Where(index => index.GetValue("collection", string.Empty) == MongoCollectionNames.AlertDispatchRequests)));
        notificationIndexes
            .Setup(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BsonDocumentCursor(existingIndexes.Where(index => index.GetValue("collection", string.Empty) == MongoCollectionNames.NotificationDeliveryAttempts)));
        alertAcknowledgementIndexes
            .Setup(indexManager => indexManager.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BsonDocumentCursor(existingIndexes.Where(index => index.GetValue("collection", string.Empty) == MongoCollectionNames.AlertAcknowledgements)));

        return new TestMongoIndexes(database, userIndexes, refreshTokenIndexes, driverProfileIndexes, driverVehicleIndexes, emergencyContactIndexes, deviceActivationCodeIndexes, userDeviceIndexes, userSubscriptionIndexes, onboardingConfirmationIndexes, tripIndexes, offlineIngestionIndexes, incidentIndexes, alertDispatchIndexes, notificationIndexes, alertAcknowledgementIndexes);
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

    private static BsonDocument DeviceActivationCodeUserIdIndex(string name) => Index(MongoCollectionNames.DeviceActivationCodes, name, new BsonDocument(nameof(DeviceActivationCode.UserId), 1), unique: false);
    private static BsonDocument DeviceActivationCodeCodeIndex(string name) => Index(MongoCollectionNames.DeviceActivationCodes, name, new BsonDocument(nameof(DeviceActivationCode.Code), 1), unique: false);
    private static BsonDocument DeviceActivationCodeExpiresAtIndex(string name) => Index(MongoCollectionNames.DeviceActivationCodes, name, new BsonDocument(nameof(DeviceActivationCode.ExpiresAtUtc), 1), unique: false);
    private static BsonDocument DeviceActivationCodeUserUsedRevokedIndex(string name) => Index(
        MongoCollectionNames.DeviceActivationCodes,
        name,
        new BsonDocument
        {
            [nameof(DeviceActivationCode.UserId)] = 1,
            [nameof(DeviceActivationCode.IsUsed)] = 1,
            [nameof(DeviceActivationCode.IsRevoked)] = 1
        },
        unique: false);

    private static BsonDocument UserDeviceUserIdIndex(string name) => Index(MongoCollectionNames.UserDevices, name, new BsonDocument(nameof(UserDevice.UserId), 1), unique: false);
    private static BsonDocument UserDeviceParentDeviceIdIndex(string name) => Index(MongoCollectionNames.UserDevices, name, new BsonDocument(nameof(UserDevice.ParentDeviceId), 1), unique: false);
    private static BsonDocument UserDeviceLinkStatusIndex(string name) => Index(MongoCollectionNames.UserDevices, name, new BsonDocument(nameof(UserDevice.LinkStatus), 1), unique: false);
    private static BsonDocument UserDeviceIdentifierHashIndex(string name) => Index(MongoCollectionNames.UserDevices, name, new BsonDocument(nameof(UserDevice.DeviceIdentifierHash), 1), unique: false);
    private static BsonDocument UserDeviceUserIdIsActiveIndex(string name) => Index(
        MongoCollectionNames.UserDevices,
        name,
        new BsonDocument
        {
            [nameof(UserDevice.UserId)] = 1,
            [nameof(UserDevice.IsActive)] = 1
        },
        unique: false);
    private static BsonDocument UserDeviceUserIdDeviceTypeIndex(string name) => Index(
        MongoCollectionNames.UserDevices,
        name,
        new BsonDocument
        {
            [nameof(UserDevice.UserId)] = 1,
            [nameof(UserDevice.DeviceType)] = 1
        },
        unique: false);

    private static BsonDocument UserSubscriptionUserIdIndex(string name) => Index(MongoCollectionNames.UserSubscriptions, name, new BsonDocument(nameof(UserSubscription.UserId), 1), unique: false);
    private static BsonDocument UserSubscriptionPlanTierIndex(string name) => Index(MongoCollectionNames.UserSubscriptions, name, new BsonDocument(nameof(UserSubscription.PlanTier), 1), unique: false);
    private static BsonDocument UserSubscriptionSourceIndex(string name) => Index(MongoCollectionNames.UserSubscriptions, name, new BsonDocument(nameof(UserSubscription.Source), 1), unique: false);
    private static BsonDocument UserSubscriptionUserIdStatusIndex(string name) => Index(
        MongoCollectionNames.UserSubscriptions,
        name,
        new BsonDocument
        {
            [nameof(UserSubscription.UserId)] = 1,
            [nameof(UserSubscription.Status)] = 1
        },
        unique: false);

    private static BsonDocument OnboardingConfirmationUserIdIndex(string name) => Index(MongoCollectionNames.OnboardingConfirmations, name, new BsonDocument(nameof(OnboardingConfirmation.UserId), 1), unique: false);
    private static BsonDocument OnboardingConfirmationIsOperationalIndex(string name) => Index(MongoCollectionNames.OnboardingConfirmations, name, new BsonDocument(nameof(OnboardingConfirmation.IsOperational), 1), unique: false);
    private static BsonDocument OnboardingConfirmationConfirmedAtIndex(string name) => Index(MongoCollectionNames.OnboardingConfirmations, name, new BsonDocument(nameof(OnboardingConfirmation.ConfirmedAtUtc), 1), unique: false);

    private static BsonDocument TripUserIdIndex(string name) => Index(MongoCollectionNames.Trips, name, new BsonDocument(nameof(Trip.UserId), 1), unique: false);
    private static BsonDocument TripVehicleIdIndex(string name) => Index(MongoCollectionNames.Trips, name, new BsonDocument(nameof(Trip.VehicleId), 1), unique: false);
    private static BsonDocument TripMobileDeviceIdIndex(string name) => Index(MongoCollectionNames.Trips, name, new BsonDocument(nameof(Trip.MobileDeviceId), 1), unique: false);
    private static BsonDocument TripStartedAtIndex(string name) => Index(MongoCollectionNames.Trips, name, new BsonDocument(nameof(Trip.StartedAtUtc), 1), unique: false);
    private static BsonDocument TripFinishedAtIndex(string name) => Index(MongoCollectionNames.Trips, name, new BsonDocument(nameof(Trip.FinishedAtUtc), 1), unique: false);
    private static BsonDocument TripUserIdStatusIndex(string name) => Index(
        MongoCollectionNames.Trips,
        name,
        new BsonDocument
        {
            [nameof(Trip.UserId)] = 1,
            [nameof(Trip.Status)] = 1
        },
        unique: false);

    private static BsonDocument OfflineIngestionUserIdIndex(string name) => Index(MongoCollectionNames.OfflineIngestionRecords, name, new BsonDocument(nameof(OfflineIngestionRecord.UserId), 1), unique: false);
    private static BsonDocument OfflineIngestionMobileDeviceIdIndex(string name) => Index(MongoCollectionNames.OfflineIngestionRecords, name, new BsonDocument(nameof(OfflineIngestionRecord.MobileDeviceId), 1), unique: false);
    private static BsonDocument OfflineIngestionTripIdIndex(string name) => Index(MongoCollectionNames.OfflineIngestionRecords, name, new BsonDocument(nameof(OfflineIngestionRecord.TripId), 1), unique: false);
    private static BsonDocument OfflineIngestionBatchIdIndex(string name) => Index(MongoCollectionNames.OfflineIngestionRecords, name, new BsonDocument(nameof(OfflineIngestionRecord.BatchId), 1), unique: false);
    private static BsonDocument OfflineIngestionClientEventIdIndex(string name) => Index(MongoCollectionNames.OfflineIngestionRecords, name, new BsonDocument(nameof(OfflineIngestionRecord.ClientEventId), 1), unique: false);
    private static BsonDocument OfflineIngestionTypeIndex(string name) => Index(MongoCollectionNames.OfflineIngestionRecords, name, new BsonDocument(nameof(OfflineIngestionRecord.Type), 1), unique: false);
    private static BsonDocument OfflineIngestionIdempotencyKeyIndex(string name) => Index(MongoCollectionNames.OfflineIngestionRecords, name, new BsonDocument(nameof(OfflineIngestionRecord.IdempotencyKey), 1), unique: true);
    private static BsonDocument OfflineIngestionAckIdIndex(string name) => Index(MongoCollectionNames.OfflineIngestionRecords, name, new BsonDocument(nameof(OfflineIngestionRecord.AckId), 1), unique: false);
    private static BsonDocument OfflineIngestionProcessingStatusIndex(string name) => Index(MongoCollectionNames.OfflineIngestionRecords, name, new BsonDocument(nameof(OfflineIngestionRecord.ProcessingStatus), 1), unique: false);
    private static BsonDocument OfflineIngestionReceivedAtIndex(string name) => Index(MongoCollectionNames.OfflineIngestionRecords, name, new BsonDocument(nameof(OfflineIngestionRecord.ReceivedAtUtc), 1), unique: false);
    private static BsonDocument OfflineIngestionOccurredAtIndex(string name) => Index(MongoCollectionNames.OfflineIngestionRecords, name, new BsonDocument(nameof(OfflineIngestionRecord.OccurredAtUtc), 1), unique: false);

    private static BsonDocument IncidentUserIdIndex(string name) => Index(MongoCollectionNames.Incidents, name, new BsonDocument(nameof(Incident.UserId), 1), unique: false);
    private static BsonDocument IncidentTripIdIndex(string name) => Index(MongoCollectionNames.Incidents, name, new BsonDocument(nameof(Incident.TripId), 1), unique: false);
    private static BsonDocument IncidentClientIncidentIdIndex(string name) => Index(MongoCollectionNames.Incidents, name, new BsonDocument(nameof(Incident.ClientIncidentId), 1), unique: false);
    private static BsonDocument IncidentIdempotencyKeyIndex(string name) => Index(MongoCollectionNames.Incidents, name, new BsonDocument(nameof(Incident.IdempotencyKey), 1), unique: true);
    private static BsonDocument IncidentOccurredAtIndex(string name) => Index(MongoCollectionNames.Incidents, name, new BsonDocument(nameof(Incident.OccurredAtUtc), 1), unique: false);
    private static BsonDocument IncidentCreatedAtIndex(string name) => Index(MongoCollectionNames.Incidents, name, new BsonDocument(nameof(Incident.CreatedAtUtc), 1), unique: false);
    private static BsonDocument IncidentClosedAtIndex(string name) => Index(MongoCollectionNames.Incidents, name, new BsonDocument(nameof(Incident.ClosedAtUtc), 1), unique: false);
    private static BsonDocument IncidentUserIdStatusIndex(string name) => Index(MongoCollectionNames.Incidents, name, new BsonDocument { [nameof(Incident.UserId)] = 1, [nameof(Incident.Status)] = 1 }, unique: false);

    private static BsonDocument AlertDispatchUserIdIndex(string name) => Index(MongoCollectionNames.AlertDispatchRequests, name, new BsonDocument(nameof(AlertDispatchRequest.UserId), 1), unique: false);
    private static BsonDocument AlertDispatchIncidentIdIndex(string name) => Index(MongoCollectionNames.AlertDispatchRequests, name, new BsonDocument(nameof(AlertDispatchRequest.IncidentId), 1), unique: false);
    private static BsonDocument AlertDispatchClientAlertRequestIdIndex(string name) => Index(MongoCollectionNames.AlertDispatchRequests, name, new BsonDocument(nameof(AlertDispatchRequest.ClientAlertRequestId), 1), unique: false);
    private static BsonDocument AlertDispatchIdempotencyKeyIndex(string name) => Index(MongoCollectionNames.AlertDispatchRequests, name, new BsonDocument(nameof(AlertDispatchRequest.IdempotencyKey), 1), unique: true);
    private static BsonDocument AlertDispatchRequestedAtIndex(string name) => Index(MongoCollectionNames.AlertDispatchRequests, name, new BsonDocument(nameof(AlertDispatchRequest.RequestedAtUtc), 1), unique: false);
    private static BsonDocument AlertDispatchCreatedAtIndex(string name) => Index(MongoCollectionNames.AlertDispatchRequests, name, new BsonDocument(nameof(AlertDispatchRequest.CreatedAtUtc), 1), unique: false);
    private static BsonDocument AlertDispatchCancelledAtIndex(string name) => Index(MongoCollectionNames.AlertDispatchRequests, name, new BsonDocument(nameof(AlertDispatchRequest.CancelledAtUtc), 1), unique: false);
    private static BsonDocument AlertDispatchCompletedAtIndex(string name) => Index(MongoCollectionNames.AlertDispatchRequests, name, new BsonDocument(nameof(AlertDispatchRequest.CompletedAtUtc), 1), unique: false);
    private static BsonDocument AlertDispatchUserIdStatusIndex(string name) => Index(MongoCollectionNames.AlertDispatchRequests, name, new BsonDocument { [nameof(AlertDispatchRequest.UserId)] = 1, [nameof(AlertDispatchRequest.Status)] = 1 }, unique: false);

    private static BsonDocument NotificationUserIdIndex(string name) => Index(MongoCollectionNames.NotificationDeliveryAttempts, name, new BsonDocument(nameof(NotificationDeliveryAttempt.UserId), 1), unique: false);
    private static BsonDocument NotificationAlertDispatchIdIndex(string name) => Index(MongoCollectionNames.NotificationDeliveryAttempts, name, new BsonDocument(nameof(NotificationDeliveryAttempt.AlertDispatchId), 1), unique: false);
    private static BsonDocument NotificationIncidentIdIndex(string name) => Index(MongoCollectionNames.NotificationDeliveryAttempts, name, new BsonDocument(nameof(NotificationDeliveryAttempt.IncidentId), 1), unique: false);
    private static BsonDocument NotificationEmergencyContactIdIndex(string name) => Index(MongoCollectionNames.NotificationDeliveryAttempts, name, new BsonDocument(nameof(NotificationDeliveryAttempt.EmergencyContactId), 1), unique: false);
    private static BsonDocument NotificationUserIdStatusIndex(string name) => Index(MongoCollectionNames.NotificationDeliveryAttempts, name, new BsonDocument { [nameof(NotificationDeliveryAttempt.UserId)] = 1, [nameof(NotificationDeliveryAttempt.Status)] = 1 }, unique: false);
    private static BsonDocument NotificationChannelIndex(string name) => Index(MongoCollectionNames.NotificationDeliveryAttempts, name, new BsonDocument(nameof(NotificationDeliveryAttempt.Channel), 1), unique: false);
    private static BsonDocument NotificationIdempotencyKeyIndex(string name) => Index(MongoCollectionNames.NotificationDeliveryAttempts, name, new BsonDocument(nameof(NotificationDeliveryAttempt.IdempotencyKey), 1), unique: true);
    private static BsonDocument NotificationPreparedAtIndex(string name) => Index(MongoCollectionNames.NotificationDeliveryAttempts, name, new BsonDocument(nameof(NotificationDeliveryAttempt.PreparedAtUtc), 1), unique: false);
    private static BsonDocument NotificationSimulatedSentAtIndex(string name) => Index(MongoCollectionNames.NotificationDeliveryAttempts, name, new BsonDocument(nameof(NotificationDeliveryAttempt.SimulatedSentAtUtc), 1), unique: false);
    private static BsonDocument NotificationFailedAtIndex(string name) => Index(MongoCollectionNames.NotificationDeliveryAttempts, name, new BsonDocument(nameof(NotificationDeliveryAttempt.FailedAtUtc), 1), unique: false);
    private static BsonDocument NotificationCancelledAtIndex(string name) => Index(MongoCollectionNames.NotificationDeliveryAttempts, name, new BsonDocument(nameof(NotificationDeliveryAttempt.CancelledAtUtc), 1), unique: false);
    private static BsonDocument NotificationCreatedAtIndex(string name) => Index(MongoCollectionNames.NotificationDeliveryAttempts, name, new BsonDocument(nameof(NotificationDeliveryAttempt.CreatedAtUtc), 1), unique: false);

    private static BsonDocument AlertAcknowledgementUserIdIndex(string name) => Index(MongoCollectionNames.AlertAcknowledgements, name, new BsonDocument(nameof(AlertAcknowledgement.UserId), 1), unique: false);
    private static BsonDocument AlertAcknowledgementMonitorUserIdIndex(string name) => Index(MongoCollectionNames.AlertAcknowledgements, name, new BsonDocument(nameof(AlertAcknowledgement.MonitorUserId), 1), unique: false);
    private static BsonDocument AlertAcknowledgementEmergencyContactIdIndex(string name) => Index(MongoCollectionNames.AlertAcknowledgements, name, new BsonDocument(nameof(AlertAcknowledgement.EmergencyContactId), 1), unique: false);
    private static BsonDocument AlertAcknowledgementAlertDispatchIdIndex(string name) => Index(MongoCollectionNames.AlertAcknowledgements, name, new BsonDocument(nameof(AlertAcknowledgement.AlertDispatchId), 1), unique: false);
    private static BsonDocument AlertAcknowledgementNotificationDeliveryAttemptIdIndex(string name) => Index(MongoCollectionNames.AlertAcknowledgements, name, new BsonDocument(nameof(AlertAcknowledgement.NotificationDeliveryAttemptId), 1), unique: false);
    private static BsonDocument AlertAcknowledgementIncidentIdIndex(string name) => Index(MongoCollectionNames.AlertAcknowledgements, name, new BsonDocument(nameof(AlertAcknowledgement.IncidentId), 1), unique: false);
    private static BsonDocument AlertAcknowledgementTripIdIndex(string name) => Index(MongoCollectionNames.AlertAcknowledgements, name, new BsonDocument(nameof(AlertAcknowledgement.TripId), 1), unique: false);
    private static BsonDocument AlertAcknowledgementStatusIndex(string name) => Index(MongoCollectionNames.AlertAcknowledgements, name, new BsonDocument(nameof(AlertAcknowledgement.Status), 1), unique: false);
    private static BsonDocument AlertAcknowledgementMonitorUserIdStatusIndex(string name) => Index(MongoCollectionNames.AlertAcknowledgements, name, new BsonDocument { [nameof(AlertAcknowledgement.MonitorUserId)] = 1, [nameof(AlertAcknowledgement.Status)] = 1 }, unique: false);
    private static BsonDocument AlertAcknowledgementUserIdStatusIndex(string name) => Index(MongoCollectionNames.AlertAcknowledgements, name, new BsonDocument { [nameof(AlertAcknowledgement.UserId)] = 1, [nameof(AlertAcknowledgement.Status)] = 1 }, unique: false);
    private static BsonDocument AlertAcknowledgementIdempotencyKeyIndex(string name) => Index(MongoCollectionNames.AlertAcknowledgements, name, new BsonDocument(nameof(AlertAcknowledgement.IdempotencyKey), 1), unique: true);
    private static BsonDocument AlertAcknowledgementCreatedAtIndex(string name) => Index(MongoCollectionNames.AlertAcknowledgements, name, new BsonDocument(nameof(AlertAcknowledgement.CreatedAtUtc), 1), unique: false);
    private static BsonDocument AlertAcknowledgementViewedAtIndex(string name) => Index(MongoCollectionNames.AlertAcknowledgements, name, new BsonDocument(nameof(AlertAcknowledgement.ViewedAtUtc), 1), unique: false);
    private static BsonDocument AlertAcknowledgementAcknowledgedAtIndex(string name) => Index(MongoCollectionNames.AlertAcknowledgements, name, new BsonDocument(nameof(AlertAcknowledgement.AcknowledgedAtUtc), 1), unique: false);
    private static BsonDocument AlertAcknowledgementDeclinedAtIndex(string name) => Index(MongoCollectionNames.AlertAcknowledgements, name, new BsonDocument(nameof(AlertAcknowledgement.DeclinedAtUtc), 1), unique: false);

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
        Mock<IMongoIndexManager<EmergencyContact>> EmergencyContactIndexes,
        Mock<IMongoIndexManager<DeviceActivationCode>> DeviceActivationCodeIndexes,
        Mock<IMongoIndexManager<UserDevice>> UserDeviceIndexes,
        Mock<IMongoIndexManager<UserSubscription>> UserSubscriptionIndexes,
        Mock<IMongoIndexManager<OnboardingConfirmation>> OnboardingConfirmationIndexes,
        Mock<IMongoIndexManager<Trip>> TripIndexes,
        Mock<IMongoIndexManager<OfflineIngestionRecord>> OfflineIngestionIndexes,
        Mock<IMongoIndexManager<Incident>> IncidentIndexes,
        Mock<IMongoIndexManager<AlertDispatchRequest>> AlertDispatchIndexes,
        Mock<IMongoIndexManager<NotificationDeliveryAttempt>> NotificationIndexes,
        Mock<IMongoIndexManager<AlertAcknowledgement>> AlertAcknowledgementIndexes);

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
