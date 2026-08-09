namespace MotoSOS.API.Modules.EmergencyStatus.Contracts;

public sealed record EmergencyNotificationSummaryResponse(int Total, int Prepared, int SimulatedSent, int Failed, int Cancelled);
