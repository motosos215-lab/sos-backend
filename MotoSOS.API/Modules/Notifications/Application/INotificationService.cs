using MotoSOS.API.Modules.Notifications.Contracts;

namespace MotoSOS.API.Modules.Notifications.Application;

public interface INotificationService
{
    Task<PrepareNotificationAttemptsResponse> PrepareAsync(string userId, PrepareNotificationAttemptsRequest request, CancellationToken cancellationToken);
    Task<GetNotificationDeliveryAttemptsResponse> ListAsync(string userId, string? alertDispatchId, string? incidentId, string? status, int? pageNumber, int? pageSize, CancellationToken cancellationToken);
    Task<GetNotificationDeliveryAttemptResponse> GetAsync(string userId, string id, CancellationToken cancellationToken);
    Task<MarkNotificationSimulatedSentResponse> MarkSimulatedSentAsync(string userId, string id, MarkNotificationSimulatedSentRequest request, CancellationToken cancellationToken);
    Task<MarkNotificationFailedResponse> MarkFailedAsync(string userId, string id, MarkNotificationFailedRequest request, CancellationToken cancellationToken);
    Task<CancelNotificationAttemptResponse> CancelAsync(string userId, string id, CancelNotificationAttemptRequest request, CancellationToken cancellationToken);
}
