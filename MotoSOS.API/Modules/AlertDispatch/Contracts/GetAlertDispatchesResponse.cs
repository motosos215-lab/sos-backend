namespace MotoSOS.API.Modules.AlertDispatch.Contracts;

public sealed record GetAlertDispatchesResponse(IReadOnlyList<AlertDispatchResponse> AlertDispatches, int PageNumber, int PageSize, long TotalCount);
