using MotoSOS.API.Modules.Auth.Domain;

namespace MotoSOS.API.Modules.Auth.Application;

public interface IAuthCodeDeliveryProvider
{
    AuthCodeDeliveryChannel Channel { get; }

    Task<AuthCodeDeliveryStatus> DeliverAsync(string emailNormalized, AuthCodePurpose purpose, string code, CancellationToken cancellationToken);
}
