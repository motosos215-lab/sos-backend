namespace MotoSOS.API.Modules.OperationalDashboard.Contracts;

public sealed record OperationalDashboardOfflineProcessingSummaryResponse(long PendingProcessing, long Processing, long Processed, long Ignored, long FailedPermanent);
