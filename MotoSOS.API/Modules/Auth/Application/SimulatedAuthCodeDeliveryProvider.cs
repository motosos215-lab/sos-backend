using MotoSOS.API.Modules.Auth.Domain;

namespace MotoSOS.API.Modules.Auth.Application;

public sealed class SimulatedAuthCodeDeliveryProvider : IAuthCodeDeliveryProvider
{
    private readonly ILogger<SimulatedAuthCodeDeliveryProvider> _logger;

    public SimulatedAuthCodeDeliveryProvider(ILogger<SimulatedAuthCodeDeliveryProvider> logger)
    {
        _logger = logger;
    }

    public AuthCodeDeliveryChannel Channel => AuthCodeDeliveryChannel.Simulated;

    public Task<AuthCodeDeliveryStatus> DeliverAsync(string emailNormalized, AuthCodePurpose purpose, string code, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Auth code delivery simulated for purpose {Purpose}.", purpose);
        return Task.FromResult(AuthCodeDeliveryStatus.Delivered);
    }
}
