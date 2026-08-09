namespace MotoSOS.API.Modules.AlertAcknowledgements.Contracts;

public sealed record GetAlertAcknowledgementsResponse(IReadOnlyList<AlertAcknowledgementResponse> Acknowledgements, int PageNumber, int PageSize, long TotalCount);
