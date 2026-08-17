using MongoDB.Bson;
using MongoDB.Driver;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Collections;
using MotoSOS.API.Modules.AlertAcknowledgements.Domain;
using MotoSOS.API.Modules.AlertDispatch.Domain;
using MotoSOS.API.Modules.AuditLogRetention.Domain;
using MotoSOS.API.Modules.AuditLogs.Domain;
using MotoSOS.API.Modules.Auth.Domain;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.EmergencyResolution.Domain;
using MotoSOS.API.Modules.Escalations.Domain;
using MotoSOS.API.Modules.EvidenceAttachments.Domain;
using MotoSOS.API.Modules.Incidents.Domain;
using MotoSOS.API.Modules.LocationSharing.Domain;
using MotoSOS.API.Modules.MinorEvents.Domain;
using MotoSOS.API.Modules.NotificationPreferences.Domain;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.OfflineIngestion.Domain;
using MotoSOS.API.Modules.Onboarding.Domain;
using MotoSOS.API.Modules.Plans.Domain;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;
using MotoSOS.API.Modules.ReportExports.Domain;
using MotoSOS.API.Modules.TelemetrySummary.Domain;
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

        IMongoCollection<AuthCode> authCodes = _database.GetCollection<AuthCode>(MongoCollectionNames.AuthCodes);
        await EnsureIndexAsync(authCodes, "ix_authCodes_emailNormalized_purpose_status", new BsonDocument { [nameof(AuthCode.EmailNormalized)] = 1, [nameof(AuthCode.Purpose)] = 1, [nameof(AuthCode.Status)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(authCodes, "ix_authCodes_userId_purpose_status", new BsonDocument { [nameof(AuthCode.UserId)] = 1, [nameof(AuthCode.Purpose)] = 1, [nameof(AuthCode.Status)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(authCodes, "ix_authCodes_expiresAtUtc", new BsonDocument(nameof(AuthCode.ExpiresAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(authCodes, "ix_authCodes_createdAtUtc", new BsonDocument(nameof(AuthCode.CreatedAtUtc), 1), unique: false, cancellationToken);

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

        IMongoCollection<TripRoutePoint> tripRoutePoints = _database.GetCollection<TripRoutePoint>(MongoCollectionNames.TripRoutePoints);
        await EnsureIndexAsync(tripRoutePoints, "ux_tripRoutePoints_tripId_clientRoutePointId", new BsonDocument { [nameof(TripRoutePoint.TripId)] = 1, [nameof(TripRoutePoint.ClientRoutePointId)] = 1 }, unique: true, cancellationToken);
        await EnsureIndexAsync(tripRoutePoints, "ix_tripRoutePoints_tripId_sequence", new BsonDocument { [nameof(TripRoutePoint.TripId)] = 1, [nameof(TripRoutePoint.Sequence)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(tripRoutePoints, "ix_tripRoutePoints_userId_tripId", new BsonDocument { [nameof(TripRoutePoint.UserId)] = 1, [nameof(TripRoutePoint.TripId)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(tripRoutePoints, "ix_tripRoutePoints_tripId_recordedAtUtc", new BsonDocument { [nameof(TripRoutePoint.TripId)] = 1, [nameof(TripRoutePoint.RecordedAtUtc)] = 1 }, unique: false, cancellationToken);

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
        await EnsureIndexAsync(offlineIngestionRecords, "ix_offlineIngestionRecords_processingStatus_processingStartedAtUtc", new BsonDocument { [nameof(OfflineIngestionRecord.ProcessingStatus)] = 1, [nameof(OfflineIngestionRecord.ProcessingStartedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(offlineIngestionRecords, "ix_offlineIngestionRecords_receivedAtUtc", new BsonDocument(nameof(OfflineIngestionRecord.ReceivedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(offlineIngestionRecords, "ix_offlineIngestionRecords_occurredAtUtc", new BsonDocument(nameof(OfflineIngestionRecord.OccurredAtUtc), 1), unique: false, cancellationToken);

        IMongoCollection<Incident> incidents = _database.GetCollection<Incident>(MongoCollectionNames.Incidents);
        await EnsureIndexAsync(incidents, "ix_incidents_userId", new BsonDocument(nameof(Incident.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(incidents, "ix_incidents_tripId", new BsonDocument(nameof(Incident.TripId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(incidents, "ix_incidents_userId_status", new BsonDocument { [nameof(Incident.UserId)] = 1, [nameof(Incident.Status)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(incidents, "ix_incidents_clientIncidentId", new BsonDocument(nameof(Incident.ClientIncidentId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(incidents, "ux_incidents_idempotencyKey", new BsonDocument(nameof(Incident.IdempotencyKey), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(incidents, "ix_incidents_occurredAtUtc", new BsonDocument(nameof(Incident.OccurredAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(incidents, "ix_incidents_createdAtUtc", new BsonDocument(nameof(Incident.CreatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(incidents, "ix_incidents_closedAtUtc", new BsonDocument(nameof(Incident.ClosedAtUtc), 1), unique: false, cancellationToken);

        IMongoCollection<AlertDispatchRequest> alertDispatchRequests = _database.GetCollection<AlertDispatchRequest>(MongoCollectionNames.AlertDispatchRequests);
        await EnsureIndexAsync(alertDispatchRequests, "ix_alertDispatchRequests_userId", new BsonDocument(nameof(AlertDispatchRequest.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertDispatchRequests, "ix_alertDispatchRequests_incidentId", new BsonDocument(nameof(AlertDispatchRequest.IncidentId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertDispatchRequests, "ix_alertDispatchRequests_userId_status", new BsonDocument { [nameof(AlertDispatchRequest.UserId)] = 1, [nameof(AlertDispatchRequest.Status)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(alertDispatchRequests, "ix_alertDispatchRequests_clientAlertRequestId", new BsonDocument(nameof(AlertDispatchRequest.ClientAlertRequestId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertDispatchRequests, "ux_alertDispatchRequests_idempotencyKey", new BsonDocument(nameof(AlertDispatchRequest.IdempotencyKey), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(alertDispatchRequests, "ix_alertDispatchRequests_requestedAtUtc", new BsonDocument(nameof(AlertDispatchRequest.RequestedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertDispatchRequests, "ix_alertDispatchRequests_createdAtUtc", new BsonDocument(nameof(AlertDispatchRequest.CreatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertDispatchRequests, "ix_alertDispatchRequests_cancelledAtUtc", new BsonDocument(nameof(AlertDispatchRequest.CancelledAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertDispatchRequests, "ix_alertDispatchRequests_completedAtUtc", new BsonDocument(nameof(AlertDispatchRequest.CompletedAtUtc), 1), unique: false, cancellationToken);

        IMongoCollection<NotificationDeliveryAttempt> notificationDeliveryAttempts = _database.GetCollection<NotificationDeliveryAttempt>(MongoCollectionNames.NotificationDeliveryAttempts);
        await EnsureIndexAsync(notificationDeliveryAttempts, "ix_notificationDeliveryAttempts_userId", new BsonDocument(nameof(NotificationDeliveryAttempt.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(notificationDeliveryAttempts, "ix_notificationDeliveryAttempts_alertDispatchId", new BsonDocument(nameof(NotificationDeliveryAttempt.AlertDispatchId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(notificationDeliveryAttempts, "ix_notificationDeliveryAttempts_incidentId", new BsonDocument(nameof(NotificationDeliveryAttempt.IncidentId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(notificationDeliveryAttempts, "ix_notificationDeliveryAttempts_emergencyContactId", new BsonDocument(nameof(NotificationDeliveryAttempt.EmergencyContactId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(notificationDeliveryAttempts, "ix_notificationDeliveryAttempts_userId_status", new BsonDocument { [nameof(NotificationDeliveryAttempt.UserId)] = 1, [nameof(NotificationDeliveryAttempt.Status)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(notificationDeliveryAttempts, "ix_notificationDeliveryAttempts_channel", new BsonDocument(nameof(NotificationDeliveryAttempt.Channel), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(notificationDeliveryAttempts, "ux_notificationDeliveryAttempts_idempotencyKey", new BsonDocument(nameof(NotificationDeliveryAttempt.IdempotencyKey), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(notificationDeliveryAttempts, "ix_notificationDeliveryAttempts_preparedAtUtc", new BsonDocument(nameof(NotificationDeliveryAttempt.PreparedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(notificationDeliveryAttempts, "ix_notificationDeliveryAttempts_simulatedSentAtUtc", new BsonDocument(nameof(NotificationDeliveryAttempt.SimulatedSentAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(notificationDeliveryAttempts, "ix_notificationDeliveryAttempts_failedAtUtc", new BsonDocument(nameof(NotificationDeliveryAttempt.FailedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(notificationDeliveryAttempts, "ix_notificationDeliveryAttempts_cancelledAtUtc", new BsonDocument(nameof(NotificationDeliveryAttempt.CancelledAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(notificationDeliveryAttempts, "ix_notificationDeliveryAttempts_createdAtUtc", new BsonDocument(nameof(NotificationDeliveryAttempt.CreatedAtUtc), 1), unique: false, cancellationToken);

        IMongoCollection<PushNotificationToken> pushNotificationTokens = _database.GetCollection<PushNotificationToken>(MongoCollectionNames.PushNotificationTokens);
        await EnsureIndexAsync(pushNotificationTokens, "ix_pushNotificationTokens_userId", new BsonDocument(nameof(PushNotificationToken.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(pushNotificationTokens, "ix_pushNotificationTokens_deviceId", new BsonDocument(nameof(PushNotificationToken.DeviceId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(pushNotificationTokens, "ix_pushNotificationTokens_platform", new BsonDocument(nameof(PushNotificationToken.Platform), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(pushNotificationTokens, "ix_pushNotificationTokens_channel", new BsonDocument(nameof(PushNotificationToken.Channel), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(pushNotificationTokens, "ix_pushNotificationTokens_status", new BsonDocument(nameof(PushNotificationToken.Status), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(pushNotificationTokens, "ix_pushNotificationTokens_registeredAtUtc", new BsonDocument(nameof(PushNotificationToken.RegisteredAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(pushNotificationTokens, "ix_pushNotificationTokens_lastSeenAtUtc", new BsonDocument(nameof(PushNotificationToken.LastSeenAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(pushNotificationTokens, "ix_pushNotificationTokens_createdAtUtc", new BsonDocument(nameof(PushNotificationToken.CreatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(pushNotificationTokens, "ix_pushNotificationTokens_updatedAtUtc", new BsonDocument(nameof(PushNotificationToken.UpdatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(pushNotificationTokens, "ux_pushNotificationTokens_idempotencyKey", new BsonDocument(nameof(PushNotificationToken.IdempotencyKey), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(pushNotificationTokens, "ix_pushNotificationTokens_userId_status_lastSeenAtUtc", new BsonDocument { [nameof(PushNotificationToken.UserId)] = 1, [nameof(PushNotificationToken.Status)] = 1, [nameof(PushNotificationToken.LastSeenAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(pushNotificationTokens, "ix_pushNotificationTokens_userId_platform_channel_status", new BsonDocument { [nameof(PushNotificationToken.UserId)] = 1, [nameof(PushNotificationToken.Platform)] = 1, [nameof(PushNotificationToken.Channel)] = 1, [nameof(PushNotificationToken.Status)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(pushNotificationTokens, "ix_pushNotificationTokens_tokenHash", new BsonDocument(nameof(PushNotificationToken.TokenHash), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(pushNotificationTokens, "ix_pushNotificationTokens_deviceId_status", new BsonDocument { [nameof(PushNotificationToken.DeviceId)] = 1, [nameof(PushNotificationToken.Status)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(pushNotificationTokens, "ix_pushNotificationTokens_platform_channel_status", new BsonDocument { [nameof(PushNotificationToken.Platform)] = 1, [nameof(PushNotificationToken.Channel)] = 1, [nameof(PushNotificationToken.Status)] = 1 }, unique: false, cancellationToken);

        IMongoCollection<NotificationPreference> notificationPreferences = _database.GetCollection<NotificationPreference>(MongoCollectionNames.NotificationPreferences);
        await EnsureIndexAsync(notificationPreferences, "ux_notificationPreferences_userId", new BsonDocument(nameof(NotificationPreference.UserId), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(notificationPreferences, "ix_notificationPreferences_updatedAtUtc", new BsonDocument(nameof(NotificationPreference.UpdatedAtUtc), 1), unique: false, cancellationToken);

        IMongoCollection<AlertAcknowledgement> alertAcknowledgements = _database.GetCollection<AlertAcknowledgement>(MongoCollectionNames.AlertAcknowledgements);
        await EnsureIndexAsync(alertAcknowledgements, "ix_alertAcknowledgements_userId", new BsonDocument(nameof(AlertAcknowledgement.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertAcknowledgements, "ix_alertAcknowledgements_monitorUserId", new BsonDocument(nameof(AlertAcknowledgement.MonitorUserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertAcknowledgements, "ix_alertAcknowledgements_emergencyContactId", new BsonDocument(nameof(AlertAcknowledgement.EmergencyContactId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertAcknowledgements, "ix_alertAcknowledgements_alertDispatchId", new BsonDocument(nameof(AlertAcknowledgement.AlertDispatchId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertAcknowledgements, "ix_alertAcknowledgements_notificationDeliveryAttemptId", new BsonDocument(nameof(AlertAcknowledgement.NotificationDeliveryAttemptId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertAcknowledgements, "ix_alertAcknowledgements_incidentId", new BsonDocument(nameof(AlertAcknowledgement.IncidentId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertAcknowledgements, "ix_alertAcknowledgements_tripId", new BsonDocument(nameof(AlertAcknowledgement.TripId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertAcknowledgements, "ix_alertAcknowledgements_status", new BsonDocument(nameof(AlertAcknowledgement.Status), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertAcknowledgements, "ix_alertAcknowledgements_monitorUserId_status", new BsonDocument { [nameof(AlertAcknowledgement.MonitorUserId)] = 1, [nameof(AlertAcknowledgement.Status)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(alertAcknowledgements, "ix_alertAcknowledgements_userId_status", new BsonDocument { [nameof(AlertAcknowledgement.UserId)] = 1, [nameof(AlertAcknowledgement.Status)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(alertAcknowledgements, "ux_alertAcknowledgements_idempotencyKey", new BsonDocument(nameof(AlertAcknowledgement.IdempotencyKey), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(alertAcknowledgements, "ix_alertAcknowledgements_createdAtUtc", new BsonDocument(nameof(AlertAcknowledgement.CreatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertAcknowledgements, "ix_alertAcknowledgements_viewedAtUtc", new BsonDocument(nameof(AlertAcknowledgement.ViewedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertAcknowledgements, "ix_alertAcknowledgements_acknowledgedAtUtc", new BsonDocument(nameof(AlertAcknowledgement.AcknowledgedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(alertAcknowledgements, "ix_alertAcknowledgements_declinedAtUtc", new BsonDocument(nameof(AlertAcknowledgement.DeclinedAtUtc), 1), unique: false, cancellationToken);

        IMongoCollection<EmergencyLocationSnapshot> emergencyLocationSnapshots = _database.GetCollection<EmergencyLocationSnapshot>(MongoCollectionNames.EmergencyLocationSnapshots);
        await EnsureIndexAsync(emergencyLocationSnapshots, "ix_emergencyLocationSnapshots_userId", new BsonDocument(nameof(EmergencyLocationSnapshot.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyLocationSnapshots, "ix_emergencyLocationSnapshots_incidentId", new BsonDocument(nameof(EmergencyLocationSnapshot.IncidentId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyLocationSnapshots, "ix_emergencyLocationSnapshots_tripId", new BsonDocument(nameof(EmergencyLocationSnapshot.TripId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyLocationSnapshots, "ux_emergencyLocationSnapshots_userId_incidentId", new BsonDocument { [nameof(EmergencyLocationSnapshot.UserId)] = 1, [nameof(EmergencyLocationSnapshot.IncidentId)] = 1 }, unique: true, cancellationToken);
        await EnsureIndexAsync(emergencyLocationSnapshots, "ix_emergencyLocationSnapshots_incidentId_isActive", new BsonDocument { [nameof(EmergencyLocationSnapshot.IncidentId)] = 1, [nameof(EmergencyLocationSnapshot.IsActive)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyLocationSnapshots, "ix_emergencyLocationSnapshots_recordedAtUtc", new BsonDocument(nameof(EmergencyLocationSnapshot.RecordedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyLocationSnapshots, "ix_emergencyLocationSnapshots_receivedAtUtc", new BsonDocument(nameof(EmergencyLocationSnapshot.ReceivedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyLocationSnapshots, "ix_emergencyLocationSnapshots_updatedAtUtc", new BsonDocument(nameof(EmergencyLocationSnapshot.UpdatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyLocationSnapshots, "ix_emergencyLocationSnapshots_isActive", new BsonDocument(nameof(EmergencyLocationSnapshot.IsActive), 1), unique: false, cancellationToken);

        IMongoCollection<EmergencyResolutionReport> emergencyResolutionReports = _database.GetCollection<EmergencyResolutionReport>(MongoCollectionNames.EmergencyResolutionReports);
        await EnsureIndexAsync(emergencyResolutionReports, "ix_emergencyResolutionReports_userId", new BsonDocument(nameof(EmergencyResolutionReport.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyResolutionReports, "ux_emergencyResolutionReports_incidentId", new BsonDocument(nameof(EmergencyResolutionReport.IncidentId), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(emergencyResolutionReports, "ix_emergencyResolutionReports_tripId", new BsonDocument(nameof(EmergencyResolutionReport.TripId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyResolutionReports, "ix_emergencyResolutionReports_alertDispatchId", new BsonDocument(nameof(EmergencyResolutionReport.AlertDispatchId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyResolutionReports, "ix_emergencyResolutionReports_outcome", new BsonDocument(nameof(EmergencyResolutionReport.Outcome), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyResolutionReports, "ix_emergencyResolutionReports_userId_outcome", new BsonDocument { [nameof(EmergencyResolutionReport.UserId)] = 1, [nameof(EmergencyResolutionReport.Outcome)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyResolutionReports, "ix_emergencyResolutionReports_userId_createdAtUtc", new BsonDocument { [nameof(EmergencyResolutionReport.UserId)] = 1, [nameof(EmergencyResolutionReport.CreatedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyResolutionReports, "ux_emergencyResolutionReports_idempotencyKey", new BsonDocument(nameof(EmergencyResolutionReport.IdempotencyKey), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(emergencyResolutionReports, "ix_emergencyResolutionReports_createdAtUtc", new BsonDocument(nameof(EmergencyResolutionReport.CreatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyResolutionReports, "ix_emergencyResolutionReports_updatedAtUtc", new BsonDocument(nameof(EmergencyResolutionReport.UpdatedAtUtc), 1), unique: false, cancellationToken);

        IMongoCollection<AuditLogEntry> auditLogs = _database.GetCollection<AuditLogEntry>(MongoCollectionNames.AuditLogs);
        await EnsureIndexAsync(auditLogs, "ix_auditLogs_actorUserId", new BsonDocument(nameof(AuditLogEntry.ActorUserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogs, "ix_auditLogs_action", new BsonDocument(nameof(AuditLogEntry.Action), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogs, "ix_auditLogs_module", new BsonDocument(nameof(AuditLogEntry.Module), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogs, "ix_auditLogs_outcome", new BsonDocument(nameof(AuditLogEntry.Outcome), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogs, "ix_auditLogs_entityType", new BsonDocument(nameof(AuditLogEntry.EntityType), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogs, "ix_auditLogs_entityId", new BsonDocument(nameof(AuditLogEntry.EntityId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogs, "ix_auditLogs_createdAtUtc", new BsonDocument(nameof(AuditLogEntry.CreatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogs, "ix_auditLogs_actorUserId_createdAtUtc", new BsonDocument { [nameof(AuditLogEntry.ActorUserId)] = 1, [nameof(AuditLogEntry.CreatedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogs, "ix_auditLogs_module_createdAtUtc", new BsonDocument { [nameof(AuditLogEntry.Module)] = 1, [nameof(AuditLogEntry.CreatedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogs, "ix_auditLogs_action_createdAtUtc", new BsonDocument { [nameof(AuditLogEntry.Action)] = 1, [nameof(AuditLogEntry.CreatedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogs, "ix_auditLogs_entityType_entityId", new BsonDocument { [nameof(AuditLogEntry.EntityType)] = 1, [nameof(AuditLogEntry.EntityId)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogs, "ix_auditLogs_correlationId", new BsonDocument(nameof(AuditLogEntry.CorrelationId), 1), unique: false, cancellationToken);

        IMongoCollection<AuditLogRetentionRun> auditLogRetentionRuns = _database.GetCollection<AuditLogRetentionRun>(MongoCollectionNames.AuditLogRetentionRuns);
        await EnsureIndexAsync(auditLogRetentionRuns, "ix_auditLogRetentionRuns_requestedByUserId", new BsonDocument(nameof(AuditLogRetentionRun.RequestedByUserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogRetentionRuns, "ix_auditLogRetentionRuns_requestedByRole", new BsonDocument(nameof(AuditLogRetentionRun.RequestedByRole), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogRetentionRuns, "ix_auditLogRetentionRuns_mode", new BsonDocument(nameof(AuditLogRetentionRun.Mode), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogRetentionRuns, "ix_auditLogRetentionRuns_status", new BsonDocument(nameof(AuditLogRetentionRun.Status), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogRetentionRuns, "ix_auditLogRetentionRuns_cutoffUtc", new BsonDocument(nameof(AuditLogRetentionRun.CutoffUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogRetentionRuns, "ix_auditLogRetentionRuns_createdAtUtc", new BsonDocument(nameof(AuditLogRetentionRun.CreatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(auditLogRetentionRuns, "ix_auditLogRetentionRuns_status_createdAtUtc", new BsonDocument { [nameof(AuditLogRetentionRun.Status)] = 1, [nameof(AuditLogRetentionRun.CreatedAtUtc)] = 1 }, unique: false, cancellationToken);

        IMongoCollection<EmergencyEscalation> emergencyEscalations = _database.GetCollection<EmergencyEscalation>(MongoCollectionNames.EmergencyEscalations);
        await EnsureIndexAsync(emergencyEscalations, "ix_emergencyEscalations_userId", new BsonDocument(nameof(EmergencyEscalation.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyEscalations, "ix_emergencyEscalations_incidentId", new BsonDocument(nameof(EmergencyEscalation.IncidentId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyEscalations, "ux_emergencyEscalations_alertDispatchId", new BsonDocument(nameof(EmergencyEscalation.AlertDispatchId), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(emergencyEscalations, "ix_emergencyEscalations_status", new BsonDocument(nameof(EmergencyEscalation.Status), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyEscalations, "ix_emergencyEscalations_reason", new BsonDocument(nameof(EmergencyEscalation.Reason), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyEscalations, "ix_emergencyEscalations_level", new BsonDocument(nameof(EmergencyEscalation.Level), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyEscalations, "ix_emergencyEscalations_createdAtUtc", new BsonDocument(nameof(EmergencyEscalation.CreatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyEscalations, "ix_emergencyEscalations_updatedAtUtc", new BsonDocument(nameof(EmergencyEscalation.UpdatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyEscalations, "ux_emergencyEscalations_idempotencyKey", new BsonDocument(nameof(EmergencyEscalation.IdempotencyKey), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(emergencyEscalations, "ix_emergencyEscalations_userId_createdAtUtc", new BsonDocument { [nameof(EmergencyEscalation.UserId)] = 1, [nameof(EmergencyEscalation.CreatedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyEscalations, "ix_emergencyEscalations_status_createdAtUtc", new BsonDocument { [nameof(EmergencyEscalation.Status)] = 1, [nameof(EmergencyEscalation.CreatedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(emergencyEscalations, "ix_emergencyEscalations_incidentId_createdAtUtc", new BsonDocument { [nameof(EmergencyEscalation.IncidentId)] = 1, [nameof(EmergencyEscalation.CreatedAtUtc)] = 1 }, unique: false, cancellationToken);

        IMongoCollection<MinorEvent> minorEvents = _database.GetCollection<MinorEvent>(MongoCollectionNames.MinorEvents);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_userId", new BsonDocument(nameof(MinorEvent.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_tripId", new BsonDocument(nameof(MinorEvent.TripId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_vehicleId", new BsonDocument(nameof(MinorEvent.VehicleId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_mobileDeviceId", new BsonDocument(nameof(MinorEvent.MobileDeviceId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_smartwatchDeviceId", new BsonDocument(nameof(MinorEvent.SmartwatchDeviceId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_eventType", new BsonDocument(nameof(MinorEvent.EventType), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_severity", new BsonDocument(nameof(MinorEvent.Severity), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_status", new BsonDocument(nameof(MinorEvent.Status), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_source", new BsonDocument(nameof(MinorEvent.Source), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_occurredAtUtc", new BsonDocument(nameof(MinorEvent.OccurredAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_createdAtUtc", new BsonDocument(nameof(MinorEvent.CreatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_updatedAtUtc", new BsonDocument(nameof(MinorEvent.UpdatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ux_minorEvents_idempotencyKey", new BsonDocument(nameof(MinorEvent.IdempotencyKey), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_userId_occurredAtUtc", new BsonDocument { [nameof(MinorEvent.UserId)] = 1, [nameof(MinorEvent.OccurredAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_tripId_occurredAtUtc", new BsonDocument { [nameof(MinorEvent.TripId)] = 1, [nameof(MinorEvent.OccurredAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_userId_tripId", new BsonDocument { [nameof(MinorEvent.UserId)] = 1, [nameof(MinorEvent.TripId)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_eventType_occurredAtUtc", new BsonDocument { [nameof(MinorEvent.EventType)] = 1, [nameof(MinorEvent.OccurredAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(minorEvents, "ix_minorEvents_status_occurredAtUtc", new BsonDocument { [nameof(MinorEvent.Status)] = 1, [nameof(MinorEvent.OccurredAtUtc)] = 1 }, unique: false, cancellationToken);

        IMongoCollection<TripTelemetrySummary> tripTelemetrySummaries = _database.GetCollection<TripTelemetrySummary>(MongoCollectionNames.TripTelemetrySummaries);
        await EnsureIndexAsync(tripTelemetrySummaries, "ix_tripTelemetrySummaries_userId", new BsonDocument(nameof(TripTelemetrySummary.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(tripTelemetrySummaries, "ix_tripTelemetrySummaries_tripId", new BsonDocument(nameof(TripTelemetrySummary.TripId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(tripTelemetrySummaries, "ix_tripTelemetrySummaries_vehicleId", new BsonDocument(nameof(TripTelemetrySummary.VehicleId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(tripTelemetrySummaries, "ix_tripTelemetrySummaries_tripStatus", new BsonDocument(nameof(TripTelemetrySummary.TripStatus), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(tripTelemetrySummaries, "ix_tripTelemetrySummaries_summaryStatus", new BsonDocument(nameof(TripTelemetrySummary.SummaryStatus), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(tripTelemetrySummaries, "ix_tripTelemetrySummaries_lastComputedAtUtc", new BsonDocument(nameof(TripTelemetrySummary.LastComputedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(tripTelemetrySummaries, "ix_tripTelemetrySummaries_createdAtUtc", new BsonDocument(nameof(TripTelemetrySummary.CreatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(tripTelemetrySummaries, "ix_tripTelemetrySummaries_updatedAtUtc", new BsonDocument(nameof(TripTelemetrySummary.UpdatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(tripTelemetrySummaries, "ux_tripTelemetrySummaries_userId_tripId", new BsonDocument { [nameof(TripTelemetrySummary.UserId)] = 1, [nameof(TripTelemetrySummary.TripId)] = 1 }, unique: true, cancellationToken);
        await EnsureIndexAsync(tripTelemetrySummaries, "ix_tripTelemetrySummaries_userId_lastComputedAtUtc", new BsonDocument { [nameof(TripTelemetrySummary.UserId)] = 1, [nameof(TripTelemetrySummary.LastComputedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(tripTelemetrySummaries, "ix_tripTelemetrySummaries_tripStatus_lastComputedAtUtc", new BsonDocument { [nameof(TripTelemetrySummary.TripStatus)] = 1, [nameof(TripTelemetrySummary.LastComputedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(tripTelemetrySummaries, "ix_tripTelemetrySummaries_summaryStatus_lastComputedAtUtc", new BsonDocument { [nameof(TripTelemetrySummary.SummaryStatus)] = 1, [nameof(TripTelemetrySummary.LastComputedAtUtc)] = 1 }, unique: false, cancellationToken);

        IMongoCollection<EvidenceAttachment> evidenceAttachments = _database.GetCollection<EvidenceAttachment>(MongoCollectionNames.EvidenceAttachments);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_userId", new BsonDocument(nameof(EvidenceAttachment.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_incidentId", new BsonDocument(nameof(EvidenceAttachment.IncidentId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_alertDispatchId", new BsonDocument(nameof(EvidenceAttachment.AlertDispatchId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_emergencyResolutionReportId", new BsonDocument(nameof(EvidenceAttachment.EmergencyResolutionReportId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_tripId", new BsonDocument(nameof(EvidenceAttachment.TripId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_evidenceType", new BsonDocument(nameof(EvidenceAttachment.EvidenceType), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_source", new BsonDocument(nameof(EvidenceAttachment.Source), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_status", new BsonDocument(nameof(EvidenceAttachment.Status), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_capturedAtUtc", new BsonDocument(nameof(EvidenceAttachment.CapturedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_createdAtUtc", new BsonDocument(nameof(EvidenceAttachment.CreatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_updatedAtUtc", new BsonDocument(nameof(EvidenceAttachment.UpdatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ux_evidenceAttachments_idempotencyKey", new BsonDocument(nameof(EvidenceAttachment.IdempotencyKey), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ux_evidenceAttachments_userId_incidentId_clientEvidenceId", new BsonDocument { [nameof(EvidenceAttachment.UserId)] = 1, [nameof(EvidenceAttachment.IncidentId)] = 1, [nameof(EvidenceAttachment.ClientEvidenceId)] = 1 }, unique: true, partialFilter: new BsonDocument(nameof(EvidenceAttachment.ClientEvidenceId), new BsonDocument("$gt", string.Empty)), cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_userId_createdAtUtc", new BsonDocument { [nameof(EvidenceAttachment.UserId)] = 1, [nameof(EvidenceAttachment.CreatedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_incidentId_createdAtUtc", new BsonDocument { [nameof(EvidenceAttachment.IncidentId)] = 1, [nameof(EvidenceAttachment.CreatedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_alertDispatchId_createdAtUtc", new BsonDocument { [nameof(EvidenceAttachment.AlertDispatchId)] = 1, [nameof(EvidenceAttachment.CreatedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_emergencyResolutionReportId_createdAtUtc", new BsonDocument { [nameof(EvidenceAttachment.EmergencyResolutionReportId)] = 1, [nameof(EvidenceAttachment.CreatedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(evidenceAttachments, "ix_evidenceAttachments_userId_status_createdAtUtc", new BsonDocument { [nameof(EvidenceAttachment.UserId)] = 1, [nameof(EvidenceAttachment.Status)] = 1, [nameof(EvidenceAttachment.CreatedAtUtc)] = 1 }, unique: false, cancellationToken);

        IMongoCollection<ResolutionReportExport> resolutionReportExports = _database.GetCollection<ResolutionReportExport>(MongoCollectionNames.ResolutionReportExports);
        await EnsureIndexAsync(resolutionReportExports, "ix_resolutionReportExports_userId", new BsonDocument(nameof(ResolutionReportExport.UserId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(resolutionReportExports, "ix_resolutionReportExports_incidentId", new BsonDocument(nameof(ResolutionReportExport.IncidentId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(resolutionReportExports, "ix_resolutionReportExports_emergencyResolutionReportId", new BsonDocument(nameof(ResolutionReportExport.EmergencyResolutionReportId), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(resolutionReportExports, "ix_resolutionReportExports_exportType", new BsonDocument(nameof(ResolutionReportExport.ExportType), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(resolutionReportExports, "ix_resolutionReportExports_status", new BsonDocument(nameof(ResolutionReportExport.Status), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(resolutionReportExports, "ix_resolutionReportExports_generatedAtUtc", new BsonDocument(nameof(ResolutionReportExport.GeneratedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(resolutionReportExports, "ix_resolutionReportExports_createdAtUtc", new BsonDocument(nameof(ResolutionReportExport.CreatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(resolutionReportExports, "ix_resolutionReportExports_updatedAtUtc", new BsonDocument(nameof(ResolutionReportExport.UpdatedAtUtc), 1), unique: false, cancellationToken);
        await EnsureIndexAsync(resolutionReportExports, "ux_resolutionReportExports_idempotencyKey", new BsonDocument(nameof(ResolutionReportExport.IdempotencyKey), 1), unique: true, cancellationToken);
        await EnsureIndexAsync(resolutionReportExports, "ix_resolutionReportExports_userId_generatedAtUtc", new BsonDocument { [nameof(ResolutionReportExport.UserId)] = 1, [nameof(ResolutionReportExport.GeneratedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(resolutionReportExports, "ix_resolutionReportExports_incidentId_generatedAtUtc", new BsonDocument { [nameof(ResolutionReportExport.IncidentId)] = 1, [nameof(ResolutionReportExport.GeneratedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(resolutionReportExports, "ix_resolutionReportExports_emergencyResolutionReportId_generatedAtUtc", new BsonDocument { [nameof(ResolutionReportExport.EmergencyResolutionReportId)] = 1, [nameof(ResolutionReportExport.GeneratedAtUtc)] = 1 }, unique: false, cancellationToken);
        await EnsureIndexAsync(resolutionReportExports, "ix_resolutionReportExports_status_generatedAtUtc", new BsonDocument { [nameof(ResolutionReportExport.Status)] = 1, [nameof(ResolutionReportExport.GeneratedAtUtc)] = 1 }, unique: false, cancellationToken);
    }

    private static async Task EnsureIndexAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        string name,
        BsonDocument key,
        bool unique,
        BsonDocument? partialFilter,
        CancellationToken cancellationToken)
    {
        if (await HasEquivalentIndexAsync(collection, key, unique, partialFilter, cancellationToken))
        {
            return;
        }

        var index = new CreateIndexModel<TDocument>(
            new BsonDocumentIndexKeysDefinition<TDocument>(key),
            new CreateIndexOptions<TDocument> { Name = name, Unique = unique, PartialFilterExpression = partialFilter is null ? null : new BsonDocumentFilterDefinition<TDocument>(partialFilter) });

        try
        {
            await collection.Indexes.CreateOneAsync(index, cancellationToken: cancellationToken);
        }
        catch (MongoCommandException exception) when (IsIndexConflict(exception))
        {
            if (await HasEquivalentIndexAsync(collection, key, unique, partialFilter, cancellationToken))
            {
                return;
            }

            throw;
        }
    }

    private static Task EnsureIndexAsync<TDocument>(IMongoCollection<TDocument> collection, string name, BsonDocument key, bool unique, CancellationToken cancellationToken) => EnsureIndexAsync(collection, name, key, unique, null, cancellationToken);

    private static async Task<bool> HasEquivalentIndexAsync<TDocument>(
        IMongoCollection<TDocument> collection,
        BsonDocument key,
        bool unique,
        BsonDocument? partialFilter,
        CancellationToken cancellationToken)
    {
        using IAsyncCursor<BsonDocument> cursor = await collection.Indexes.ListAsync(cancellationToken: cancellationToken);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (BsonDocument existingIndex in cursor.Current)
            {
                if (IsEquivalentIndex(existingIndex, key, unique, partialFilter))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsEquivalentIndex(BsonDocument existingIndex, BsonDocument key, bool unique, BsonDocument? partialFilter)
    {
        if (!existingIndex.TryGetValue("key", out BsonValue existingKey) || !existingKey.IsBsonDocument)
        {
            return false;
        }

        bool existingUnique = existingIndex.TryGetValue("unique", out BsonValue uniqueValue) && uniqueValue.ToBoolean();

        BsonDocument? existingPartial = existingIndex.TryGetValue("partialFilterExpression", out BsonValue partialValue) && partialValue.IsBsonDocument ? partialValue.AsBsonDocument : null;
        bool partialMatches = partialFilter is null ? existingPartial is null : existingPartial is not null && existingPartial.Equals(partialFilter);
        return existingKey.AsBsonDocument.Equals(key) && existingUnique == unique && partialMatches;
    }

    private static bool IsIndexConflict(MongoCommandException exception)
    {
        return exception.Code is 85 or 86 ||
            exception.Message.Contains("Index already exists with a different name", StringComparison.OrdinalIgnoreCase);
    }
}
