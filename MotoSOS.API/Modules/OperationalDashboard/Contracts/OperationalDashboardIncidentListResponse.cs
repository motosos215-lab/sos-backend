namespace MotoSOS.API.Modules.OperationalDashboard.Contracts;

public sealed record OperationalDashboardIncidentListResponse(IReadOnlyList<OperationalDashboardIncidentListItemResponse> Items, int PageNumber, int PageSize, long Total);
