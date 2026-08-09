namespace MotoSOS.API.Modules.AuditLogs.Domain;

public enum AuditAction
{
    Unknown = 0,
    AuthLogin = 1,
    AuthLogout = 2,
    IncidentCreated = 3,
    IncidentClosed = 4,
    IncidentCancelledFalsePositive = 5,
    AlertDispatchCreated = 6,
    AlertDispatchCancelled = 7,
    NotificationOutboxRun = 8,
    NotificationOutboxRetryFailed = 9,
    AlertAcknowledgementViewed = 10,
    AlertAcknowledgementAcknowledged = 11,
    AlertAcknowledgementDeclined = 12,
    EmergencyResolutionReportCreated = 13,
    OfflineProcessingRun = 14,
    EmergencyEscalationRequested = 15,
    EmergencyEscalationMarkedUnresolved = 16,
    EmergencyEscalationCancelled = 17,
    MinorEventRecorded = 18,
    MinorEventMarkedReviewed = 19,
    MinorEventIgnored = 20,
    MinorEventProcessedFromOfflineIngestion = 21,
    NotificationProviderSimulatedSent = 22,
    NotificationProviderSimulatedFailed = 23,
    NotificationOutboxWorkerRun = 24,
    NotificationOutboxWorkerFailed = 25,
    NotificationOutboxWorkerSkipped = 26
}
