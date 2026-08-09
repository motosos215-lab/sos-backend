namespace MotoSOS.API.Modules.NotificationOutbox.Contracts;

public sealed record GetNotificationOutboxStatusResponse(long Prepared, long SimulatedSent, long Failed, long Cancelled);
