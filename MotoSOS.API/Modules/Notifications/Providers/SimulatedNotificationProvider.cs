using MotoSOS.API.Common.Abstractions;

namespace MotoSOS.API.Modules.Notifications.Providers;

public sealed class SimulatedNotificationProvider : INotificationProvider
{
    private const string FailureCode = "simulated_failure_requested";
    private readonly IClock _clock;

    public SimulatedNotificationProvider(IClock clock)
    {
        _clock = clock;
    }

    public NotificationProviderType ProviderType => NotificationProviderType.Simulated;

    public Task<NotificationProviderResult> SendAsync(NotificationProviderRequest request, CancellationToken cancellationToken)
    {
        DateTimeOffset now = _clock.UtcNow;
        if (request.SimulateFailure)
        {
            return Task.FromResult(new NotificationProviderResult(ProviderType, request.Channel, NotificationProviderDeliveryStatus.Failed, null, "simulated-failed", FailureCode, "Simulated notification failure requested.", null, now));
        }

        return Task.FromResult(new NotificationProviderResult(ProviderType, request.Channel, NotificationProviderDeliveryStatus.Sent, $"simulated-{Guid.NewGuid():N}", "simulated-sent", null, null, now, null));
    }
}
