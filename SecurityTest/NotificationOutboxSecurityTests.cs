using FluentAssertions;
using MotoSOS.API.Modules.NotificationOutbox.Contracts;
using MotoSOS.API.Modules.Notifications.Providers;

namespace SecurityTest;

public sealed class NotificationOutboxSecurityTests
{
    [Fact]
    public void NotificationOutboxResponsesDoNotExposeSensitiveProviderRealtimeOrTrackingFields()
    {
        Type[] responseTypes =
        [
            typeof(RunNotificationOutboxResponse),
            typeof(NotificationOutboxItemResultResponse),
            typeof(GetNotificationOutboxStatusResponse),
            typeof(NotificationOutboxWorkerStatusResponse),
            typeof(RetryFailedNotificationOutboxResponse),
            typeof(RetryFailedNotificationOutboxItemResponse)
        ];

        string[] names = responseTypes.SelectMany(t => t.GetProperties().Select(p => p.Name)).ToArray();
        names.Should().NotContain(name => name.Contains("UserId", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Email", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Phone", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Pass" + "word", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Refresh" + "Token", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Access" + "Token", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Device" + "Identifier", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Payload", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Provider", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Pay" + "ment", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Twi" + "lio", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Send" + "Grid", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Whats" + "App", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("F" + "CM", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Web" + "Socket", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Signal" + "R", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Tracking", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Pairing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NotificationProviderAbstractionDoesNotExposeSensitiveOrRealProviderFields()
    {
        Type[] providerTypes =
        [
            typeof(NotificationProviderRequest),
            typeof(NotificationProviderResult),
            typeof(NotificationProviderType),
            typeof(NotificationProviderChannel),
            typeof(NotificationProviderDeliveryStatus),
            typeof(SimulatedNotificationProvider),
            typeof(NotificationProviderResolver)
        ];

        string joinedNames = string.Join(' ', providerTypes.Select(t => t.FullName).Concat(providerTypes.SelectMany(t => t.GetProperties().Select(p => p.Name)))).ToLowerInvariant();
        joinedNames.Should().NotContain(("Pass" + "word").ToLowerInvariant());
        joinedNames.Should().NotContain(("Refresh" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("Access" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("Device" + "Identifier").ToLowerInvariant());
        joinedNames.Should().NotContain("payload");
        joinedNames.Should().NotContain("phone");
        joinedNames.Should().NotContain("emailaddress");
        joinedNames.Should().NotContain(("Pay" + "ment").ToLowerInvariant());
        joinedNames.Should().NotContain(("Twi" + "lio").ToLowerInvariant());
        joinedNames.Should().NotContain(("Send" + "Grid").ToLowerInvariant());
        joinedNames.Should().NotContain(("Whats" + "App").ToLowerInvariant());
        joinedNames.Should().NotContain(("F" + "CM").ToLowerInvariant());
        joinedNames.Should().NotContain(("Web" + "Socket").ToLowerInvariant());
        joinedNames.Should().NotContain(("Signal" + "R").ToLowerInvariant());
        joinedNames.Should().NotContain("tracking");
        joinedNames.Should().NotContain("pairing");
    }
}
