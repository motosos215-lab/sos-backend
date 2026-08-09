using FluentAssertions;
using MotoSOS.API.Modules.LocationSharing.Application;
using MotoSOS.API.Modules.LocationSharing.Contracts;
using MotoSOS.API.Modules.LocationSharing.Domain;

namespace SecurityTest;

public sealed class LocationSharingSecurityTests
{
    [Fact]
    public void LocationSharingDoesNotExposeSensitiveFieldsOrUseLiveTrackingDependencies()
    {
        typeof(LocationSharingService).GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType.Name)
            .Should().NotContain(name => name.Contains("SignalR", StringComparison.OrdinalIgnoreCase) || name.Contains("WebSocket", StringComparison.OrdinalIgnoreCase) || name.Contains("Twilio", StringComparison.OrdinalIgnoreCase) || name.Contains("SendGrid", StringComparison.OrdinalIgnoreCase) || name.Contains("Fcm", StringComparison.OrdinalIgnoreCase));

        typeof(LocationSnapshotResponse).GetProperties().Select(property => property.Name)
            .Should().NotContain(name => name.Contains("Password", StringComparison.OrdinalIgnoreCase) || name.Contains("Token", StringComparison.OrdinalIgnoreCase) || name.Contains("DeviceIdentifier", StringComparison.OrdinalIgnoreCase));

        typeof(EmergencyLocationSnapshot).GetProperties().Select(property => property.Name)
            .Should().NotContain("Polyline").And.NotContain("Points").And.NotContain("RouteHistory");
    }
}
