namespace MotoSOS.API.Modules.OperationalDashboard.Contracts;

public sealed record OperationalDashboardIncidentsSummaryResponse(long Total, long Open, long Closed, long FalsePositiveCancelled);
