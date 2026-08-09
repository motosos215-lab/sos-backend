using FluentAssertions;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Notifications.Providers;

namespace UnitTest.Notifications;

public sealed class NotificationProviderResolverTests
{
    [Theory]
    [InlineData(NotificationProviderChannel.Sms)]
    [InlineData(NotificationProviderChannel.Email)]
    [InlineData(NotificationProviderChannel.Push)]
    public void SupportedChannelsResolveToSimulatedProvider(NotificationProviderChannel channel)
    {
        var resolver = new NotificationProviderResolver(new SimulatedNotificationProvider(new Clock()));
        INotificationProvider provider = resolver.Resolve(channel);
        provider.Should().BeOfType<SimulatedNotificationProvider>();
        provider.ProviderType.Should().Be(NotificationProviderType.Simulated);
    }

    [Fact]
    public void UnsupportedChannelThrowsControlledError()
    {
        var resolver = new NotificationProviderResolver(new SimulatedNotificationProvider(new Clock()));
        Assert.Throws<NotificationNotAllowedAppException>(() => resolver.Resolve((NotificationProviderChannel)99));
    }

    private sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026, 8, 8, 12, 0, 0, TimeSpan.Zero); }
}
