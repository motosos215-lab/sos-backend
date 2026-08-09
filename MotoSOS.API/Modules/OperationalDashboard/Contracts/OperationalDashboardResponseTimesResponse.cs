namespace MotoSOS.API.Modules.OperationalDashboard.Contracts;

public sealed record OperationalDashboardResponseTimesResponse(long TotalReports, long ReportsWithResponseTime, double? AverageResponseTimeSeconds, long? MinResponseTimeSeconds, long? MaxResponseTimeSeconds);
