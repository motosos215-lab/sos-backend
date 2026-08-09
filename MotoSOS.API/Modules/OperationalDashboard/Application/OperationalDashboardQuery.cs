using MotoSOS.API.Modules.Incidents.Domain;

namespace MotoSOS.API.Modules.OperationalDashboard.Application;

public sealed record OperationalDashboardQuery(DateTimeOffset? DateFrom, DateTimeOffset? DateTo, string? Status, int? PageNumber, int? PageSize)
{
    public IncidentStatus? ParsedStatus { get; init; }
}
