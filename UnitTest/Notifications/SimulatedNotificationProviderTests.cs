using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.Notifications.Providers;

namespace UnitTest.Notifications;

public sealed class SimulatedNotificationProviderTests
{
    [Fact]
    public async Task SuccessReturnsSafeSimulatedResult()
    {
        var provider = new SimulatedNotificationProvider(new Clock());
        NotificationProviderResult result = await provider.SendAsync(Request(simulateFailure: false), CancellationToken.None);
        result.ProviderType.Should().Be(NotificationProviderType.Simulated);
        result.DeliveryStatus.Should().Be(NotificationProviderDeliveryStatus.Sent);
        result.ProviderMessageId.Should().StartWith("simulated-");
        result.SentAtUtc.Should().Be(Clock.Now);
        result.ErrorCode.Should().BeNull();
        result.ToString().ToLowerInvariant().Should().NotContain("token").And.NotContain("payload");
    }

    [Fact]
    public async Task FailureReturnsControlledSimulatedResult()
    {
        var provider = new SimulatedNotificationProvider(new Clock());
        NotificationProviderResult result = await provider.SendAsync(Request(simulateFailure: true), CancellationToken.None);
        result.DeliveryStatus.Should().Be(NotificationProviderDeliveryStatus.Failed);
        result.ErrorCode.Should().Be("simulated_failure_requested");
        result.ErrorMessage.Should().Be("Simulated notification failure requested.");
        result.FailedAtUtc.Should().Be(Clock.Now);
        result.ProviderMessageId.Should().BeNull();
    }

    private static NotificationProviderRequest Request(bool simulateFailure) => new("attempt", "alert", "incident", NotificationProviderChannel.Sms, simulateFailure);
    private sealed class Clock : IClock { public static DateTimeOffset Now { get; } = new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero); public DateTimeOffset UtcNow => Now; }
}
