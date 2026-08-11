using Microsoft.Extensions.Options;
using MotoSOS.API.Modules.Auth.Domain;

namespace MotoSOS.API.Modules.Auth.Application;

public sealed class AuthCodeDeliveryProviderResolver : IAuthCodeDeliveryProvider
{
    private readonly SimulatedAuthCodeDeliveryProvider _simulated;
    private readonly EmailAuthCodeDeliveryProvider _email;
    private readonly AuthCodeOptions _options;
    private readonly AuthCodeEmailOptionsValidator _emailOptionsValidator;
    private readonly ILogger<AuthCodeDeliveryProviderResolver> _logger;

    public AuthCodeDeliveryProviderResolver(SimulatedAuthCodeDeliveryProvider simulated, EmailAuthCodeDeliveryProvider email, IOptions<AuthCodeOptions> options, AuthCodeEmailOptionsValidator emailOptionsValidator, ILogger<AuthCodeDeliveryProviderResolver> logger)
    {
        _simulated = simulated;
        _email = email;
        _options = options.Value;
        _emailOptionsValidator = emailOptionsValidator;
        _logger = logger;
    }

    public AuthCodeDeliveryChannel Channel => IsEmailProviderSelected() ? AuthCodeDeliveryChannel.Email : AuthCodeDeliveryChannel.Simulated;

    public async Task<AuthCodeDeliveryStatus> DeliverAsync(string emailNormalized, AuthCodePurpose purpose, string code, CancellationToken cancellationToken)
    {
        if (!IsEmailProviderSelected())
        {
            return await _simulated.DeliverAsync(emailNormalized, purpose, code, cancellationToken);
        }

        AuthCodeEmailConfigurationStatus status = _emailOptionsValidator.Validate(_options.Email);
        if (!status.Configured)
        {
            _logger.LogWarning("Auth code email delivery provider is not configured.");
            return AuthCodeDeliveryStatus.Failed;
        }

        return await _email.DeliverAsync(emailNormalized, purpose, code, cancellationToken);
    }

    private bool IsEmailProviderSelected() => string.Equals(_options.Provider?.Trim(), "Email", StringComparison.OrdinalIgnoreCase);
}
