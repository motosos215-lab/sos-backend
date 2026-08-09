using FluentAssertions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Contracts;

namespace SecurityTest;

public sealed class AlertAcknowledgementSecurityTests
{
    [Fact]
    public void AlertAcknowledgementsDoNotIntroduceNotificationProviderDependenciesOrSensitiveResponseFields()
    {
        typeof(AlertAcknowledgementService).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType.Name)
            .Should().NotContain(name => name.Contains("Twilio", StringComparison.OrdinalIgnoreCase) || name.Contains("SendGrid", StringComparison.OrdinalIgnoreCase) || name.Contains("Fcm", StringComparison.OrdinalIgnoreCase) || name.Contains("Payment", StringComparison.OrdinalIgnoreCase));

        typeof(AlertAcknowledgementResponse).GetProperties().Select(property => property.Name)
            .Should().NotContain(name => name.Contains("Password", StringComparison.OrdinalIgnoreCase) || name.Contains("Token", StringComparison.OrdinalIgnoreCase) || name.Contains("DeviceIdentifier", StringComparison.OrdinalIgnoreCase));
    }
}
