using FluentAssertions;
using MotoSOS.API.Modules.NotificationOutbox.Application;
using MotoSOS.API.Modules.NotificationOutbox.Contracts;

namespace UnitTest.NotificationOutbox;

public sealed class NotificationOutboxValidatorTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    public async Task RunValidatorValidatesMaxItems(int? maxItems, bool expected)
    {
        var validator = new RunNotificationOutboxRequestValidator();
        (await validator.ValidateAsync(new RunNotificationOutboxRequest(maxItems, null))).IsValid.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    public async Task RetryValidatorValidatesMaxItems(int? maxItems, bool expected)
    {
        var validator = new RetryFailedNotificationOutboxRequestValidator();
        (await validator.ValidateAsync(new RetryFailedNotificationOutboxRequest(maxItems))).IsValid.Should().Be(expected);
    }
}
