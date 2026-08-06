using MotoSOS.API.Modules.Plans.Contracts;

namespace MotoSOS.API.Modules.Plans.Application;

public interface ISubscriptionService
{
    Task<GetMySubscriptionResponse> GetMySubscriptionAsync(string userId, CancellationToken cancellationToken);
    Task<SelectBasicSubscriptionResponse> SelectBasicAsync(string userId, CancellationToken cancellationToken);
}
