namespace MotoSOS.API.Modules.OperationalDashboard.Contracts;

public sealed record OperationalDashboardNotificationsSummaryResponse(long Total, long Prepared, long SimulatedSent, long Failed, long Cancelled);
