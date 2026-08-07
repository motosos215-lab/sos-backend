using FluentAssertions;
using MotoSOS.API.Modules.EmergencyStatus.Application;
using MotoSOS.API.Modules.EmergencyStatus.Contracts;

namespace SecurityTest;

public sealed class EmergencyStatusSecurityTests
{
    [Fact]
    public void EmergencyStatusDoesNotExposeSensitiveFieldsOrUseLiveTrackingDependencies()
    {
        typeof(EmergencyStatusService).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType.Name)
            .Should().NotContain(name => name.Contains("SignalR", StringComparison.OrdinalIgnoreCase) || name.Contains("WebSocket", StringComparison.OrdinalIgnoreCase) || name.Contains("Twilio", StringComparison.OrdinalIgnoreCase) || name.Contains("SendGrid", StringComparison.OrdinalIgnoreCase) || name.Contains("Fcm", StringComparison.OrdinalIgnoreCase) || name.Contains("Stripe", StringComparison.OrdinalIgnoreCase));

        Type[] responseTypes = [typeof(EmergencyStatusResponse), typeof(EmergencyIncidentStatusResponse), typeof(EmergencyTripStatusResponse), typeof(EmergencyAlertDispatchStatusResponse), typeof(EmergencyNotificationSummaryResponse), typeof(EmergencyAcknowledgementSummaryResponse), typeof(EmergencyLocationStatusResponse), typeof(GetActiveEmergenciesResponse)];
        responseTypes.SelectMany(type => type.GetProperties()).Select(property => property.Name)
            .Should().NotContain(name => name.Contains("Password", StringComparison.OrdinalIgnoreCase) || name.Contains("RefreshToken", StringComparison.OrdinalIgnoreCase) || name.Contains("AccessToken", StringComparison.OrdinalIgnoreCase) || name.Contains("DeviceIdentifier", StringComparison.OrdinalIgnoreCase) || name.Contains("ProviderToken", StringComparison.OrdinalIgnoreCase) || name.Contains("Email", StringComparison.OrdinalIgnoreCase) || name.Contains("Phone", StringComparison.OrdinalIgnoreCase) || name.Contains("Payment", StringComparison.OrdinalIgnoreCase));
    }
}
