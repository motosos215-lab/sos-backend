namespace MotoSOS.API.Modules.OperationalDashboard.Contracts;

public sealed record OperationalDashboardAcknowledgementsSummaryResponse(long Total, long Pending, long Viewed, long Acknowledged, long Declined);
