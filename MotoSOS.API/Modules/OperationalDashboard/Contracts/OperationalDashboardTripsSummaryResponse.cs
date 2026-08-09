namespace MotoSOS.API.Modules.OperationalDashboard.Contracts;

public sealed record OperationalDashboardTripsSummaryResponse(long Total, long Active, long Finished);
