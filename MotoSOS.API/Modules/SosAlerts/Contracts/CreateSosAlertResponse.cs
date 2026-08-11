namespace MotoSOS.API.Modules.SosAlerts.Contracts;

public sealed record CreateSosAlertResponse(
    SosAlertIncidentResponse Incident,
    SosAlertDispatchResponse AlertDispatch,
    IReadOnlyList<SosAlertNotificationAttemptResponse> NotificationAttempts,
    SosAlertSummaryResponse Summary);
