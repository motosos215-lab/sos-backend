using FluentAssertions;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Domain;

namespace UnitTest.Notifications;

public sealed class NotificationIdempotencyKeyFactoryTests
{
    [Fact]
    public void SameInputProducesSameKeyAndDifferentPartsChangeIt()
    {
        var factory = new NotificationIdempotencyKeyFactory();
        string key = factory.Create("user", "alert", "contact", NotificationChannel.Sms, 1);
        factory.Create("user", "alert", "contact", NotificationChannel.Sms, 1).Should().Be(key);
        factory.Create("other", "alert", "contact", NotificationChannel.Sms, 1).Should().NotBe(key);
        factory.Create("user", "other", "contact", NotificationChannel.Sms, 1).Should().NotBe(key);
        factory.Create("user", "alert", "other", NotificationChannel.Sms, 1).Should().NotBe(key);
        factory.Create("user", "alert", "contact", NotificationChannel.Email, 1).Should().NotBe(key);
        factory.Create("user", "alert", "contact", NotificationChannel.Sms, 2).Should().NotBe(key);
        key.Should().NotContain("user").And.NotContain("alert").And.NotContain("contact");
    }
}
