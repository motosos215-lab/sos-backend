namespace MotoSOS.API.Modules.MinorEvents.Contracts;

public sealed record GetMinorEventsResponse(IReadOnlyList<MinorEventResponse> MinorEvents, int PageNumber, int PageSize, long TotalCount);
