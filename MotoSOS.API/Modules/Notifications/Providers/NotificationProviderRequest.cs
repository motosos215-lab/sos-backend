namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed record NotificationProviderRequest(string NotificationDeliveryAttemptId, string AlertDispatchId, string IncidentId, NotificationProviderChannel Channel, bool SimulateFailure);
