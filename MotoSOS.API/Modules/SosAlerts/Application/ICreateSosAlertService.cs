using MotoSOS.API.Modules.SosAlerts.Contracts;

namespace MotoSOS.API.Modules.SosAlerts.Application;

public interface ICreateSosAlertService
{
    Task<CreateSosAlertResponse> CreateAsync(string userId, CreateSosAlertRequest request, CancellationToken cancellationToken);
}
