namespace MotoSOS.API.Modules.AlertAcknowledgements.Contracts;

public sealed record AcknowledgeAlertRequest(string? ResponseType, string? Message);
