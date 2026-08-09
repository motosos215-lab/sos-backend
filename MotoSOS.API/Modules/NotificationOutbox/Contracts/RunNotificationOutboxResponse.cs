namespace MotoSOS.API.Modules.NotificationOutbox.Contracts;

public sealed record RunNotificationOutboxResponse(int Processed, int SimulatedSent, int Failed, int Skipped, IReadOnlyList<NotificationOutboxItemResultResponse> Items);
