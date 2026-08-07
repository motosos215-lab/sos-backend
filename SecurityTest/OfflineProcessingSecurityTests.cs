using FluentAssertions;
using MotoSOS.API.Modules.OfflineProcessing.Application;
using MotoSOS.API.Modules.OfflineProcessing.Contracts;

namespace SecurityTest;

public sealed class OfflineProcessingSecurityTests
{
    [Fact]
    public void OfflineProcessingDoesNotExposeSensitiveFieldsOrUseExternalProviders()
    {
        typeof(OfflineProcessingService).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType.Name)
            .Should().NotContain(name => name.Contains("SignalR", StringComparison.OrdinalIgnoreCase) || name.Contains("WebSocket", StringComparison.OrdinalIgnoreCase) || name.Contains("Twilio", StringComparison.OrdinalIgnoreCase) || name.Contains("SendGrid", StringComparison.OrdinalIgnoreCase) || name.Contains("Fcm", StringComparison.OrdinalIgnoreCase) || name.Contains("Stripe", StringComparison.OrdinalIgnoreCase));

        Type[] responseTypes = [typeof(RunOfflineProcessingResponse), typeof(OfflineProcessingItemResultResponse), typeof(GetOfflineProcessingStatusResponse)];
        responseTypes.SelectMany(type => type.GetProperties()).Select(property => property.Name)
            .Should().NotContain(name => name.Contains("Password", StringComparison.OrdinalIgnoreCase) || name.Contains("RefreshToken", StringComparison.OrdinalIgnoreCase) || name.Contains("AccessToken", StringComparison.OrdinalIgnoreCase) || name.Contains("DeviceIdentifier", StringComparison.OrdinalIgnoreCase) || name.Contains("Payload", StringComparison.OrdinalIgnoreCase) || name.Contains("ProviderToken", StringComparison.OrdinalIgnoreCase));
    }
}
