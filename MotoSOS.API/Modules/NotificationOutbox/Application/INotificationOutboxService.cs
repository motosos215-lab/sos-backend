using MotoSOS.API.Modules.NotificationOutbox.Contracts;

namespace MotoSOS.API.Modules.NotificationOutbox.Application;

public interface INotificationOutboxService
{
    Task<RunNotificationOutboxResponse> RunAsync(string adminUserId, RunNotificationOutboxRequest request, CancellationToken cancellationToken);
    Task<RunNotificationOutboxResponse> RunWorkerAsync(RunNotificationOutboxRequest request, int intervalSeconds, CancellationToken cancellationToken);
    Task<GetNotificationOutboxStatusResponse> GetStatusAsync(string adminUserId, CancellationToken cancellationToken);
    Task<NotificationOutboxWorkerStatusResponse> GetWorkerStatusAsync(string adminUserId, CancellationToken cancellationToken);
    Task<RetryFailedNotificationOutboxResponse> RetryFailedAsync(string adminUserId, RetryFailedNotificationOutboxRequest request, CancellationToken cancellationToken);
}
