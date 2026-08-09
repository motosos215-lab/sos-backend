namespace MotoSOS.API.Modules.OperationalDashboard.Contracts;

public sealed record OperationalDashboardUsersSummaryResponse(long Total, long Riders, long Monitors, long Admins);
