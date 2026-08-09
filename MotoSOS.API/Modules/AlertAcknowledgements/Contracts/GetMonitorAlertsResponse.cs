namespace MotoSOS.API.Modules.AlertAcknowledgements.Contracts;

public sealed record GetMonitorAlertsResponse(IReadOnlyList<AlertAcknowledgementResponse> Alerts, int PageNumber, int PageSize, long TotalCount);
