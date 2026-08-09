namespace MotoSOS.API.Modules.Incidents.Contracts;

public sealed record GetIncidentsResponse(IReadOnlyList<IncidentResponse> Incidents, int PageNumber, int PageSize, long TotalCount);
