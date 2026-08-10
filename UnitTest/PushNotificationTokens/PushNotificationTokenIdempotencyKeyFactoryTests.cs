using FluentAssertions;
using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.PushNotificationTokens.Domain;

namespace UnitTest.PushNotificationTokens;

public sealed class PushNotificationTokenIdempotencyKeyFactoryTests
{
    [Fact]
    public void SameInputProducesSameKeyAndAllPartsAffectKey()
    {
        var factory = new PushNotificationTokenIdempotencyKeyFactory();
        string key = factory.Create("user", "hash", PushTokenPlatform.Android, PushTokenChannel.Fcm, "device");

        key.Should().Be(factory.Create("user", "hash", PushTokenPlatform.Android, PushTokenChannel.Fcm, "device"));
        key.Should().NotBe(factory.Create("other", "hash", PushTokenPlatform.Android, PushTokenChannel.Fcm, "device"));
        key.Should().NotBe(factory.Create("user", "other", PushTokenPlatform.Android, PushTokenChannel.Fcm, "device"));
        key.Should().NotBe(factory.Create("user", "hash", PushTokenPlatform.Web, PushTokenChannel.Fcm, "device"));
        key.Should().NotBe(factory.Create("user", "hash", PushTokenPlatform.Android, PushTokenChannel.WebPush, "device"));
        key.Should().NotBe(factory.Create("user", "hash", PushTokenPlatform.Android, PushTokenChannel.Fcm, "other"));
        key.Should().NotContain("hash");
    }
}
