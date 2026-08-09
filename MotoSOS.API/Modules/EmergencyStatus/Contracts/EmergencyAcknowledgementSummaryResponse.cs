namespace MotoSOS.API.Modules.EmergencyStatus.Contracts;

public sealed record EmergencyAcknowledgementSummaryResponse(int Total, int Pending, int Viewed, int Acknowledged, int Declined);
