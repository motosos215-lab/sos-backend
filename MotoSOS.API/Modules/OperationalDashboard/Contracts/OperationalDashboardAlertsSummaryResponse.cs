namespace MotoSOS.API.Modules.OperationalDashboard.Contracts;

public sealed record OperationalDashboardAlertsSummaryResponse(long DispatchesTotal, long PendingDispatch, long Completed, long Cancelled);
