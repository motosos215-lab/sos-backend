namespace MotoSOS.API.Modules.SosAlerts.Contracts;

public sealed record SosAlertSummaryResponse(int PushPrepared, int SmsPrepared, int EmailPrepared, int TotalPrepared);
