using FluentAssertions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;

namespace UnitTest.PushNotificationTokens;

public sealed class PushNotificationTokenQueryValidatorTests
{
    [Fact]
    public void ValidQueryParsesEnumsAndDefaultsPaging()
    {
        PushNotificationTokenQuery query = new PushNotificationTokenQueryValidator().Validate("user", "Android", "Fcm", "Active", null, null, null, null);

        query.UserId.Should().Be("user");
        query.Platform.Should().Be(PushTokenPlatform.Android);
        query.Channel.Should().Be(PushTokenChannel.Fcm);
        query.Status.Should().Be(PushNotificationTokenStatus.Active);
        query.PageNumber.Should().Be(1);
        query.PageSize.Should().Be(20);
    }

    [Theory]
    [InlineData("bad", "Fcm", "Active", 1, 20)]
    [InlineData("Android", "bad", "Active", 1, 20)]
    [InlineData("Android", "Fcm", "bad", 1, 20)]
    [InlineData("Android", "Fcm", "Active", 0, 20)]
    [InlineData("Android", "Fcm", "Active", 1, 101)]
    public void InvalidQueryThrowsValidationError(string platform, string channel, string status, int pageNumber, int pageSize)
    {
        Action act = () => new PushNotificationTokenQueryValidator().Validate(null, platform, channel, status, null, null, pageNumber, pageSize);

        act.Should().Throw<ValidationAppException>();
    }

    [Fact]
    public void DateFromAfterDateToThrowsValidationError()
    {
        Action act = () => new PushNotificationTokenQueryValidator().Validate(null, null, null, null, Now, Now.AddDays(-1), 1, 20);

        act.Should().Throw<ValidationAppException>();
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);
}
