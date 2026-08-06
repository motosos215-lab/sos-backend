namespace MotoSOS.API.Modules.AlertAcknowledgements.Contracts;

public sealed record DeclineAlertRequest(string? ResponseType, string? Message);
