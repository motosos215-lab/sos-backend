namespace MotoSOS.API.Modules.ReportExports.Contracts;

public sealed record ResolutionReportAlertSummaryResponse(string? AlertDispatchId, string? AlertDispatchStatus, int ContactsCount, int NotificationsTotal, int NotificationsPrepared, int NotificationsSimulatedSent, int NotificationsFailed, int NotificationsCancelled);
