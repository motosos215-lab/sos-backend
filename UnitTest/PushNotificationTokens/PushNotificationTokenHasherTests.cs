using FluentAssertions;
using MotoSOS.API.Modules.PushNotificationTokens.Application;

namespace UnitTest.PushNotificationTokens;

public sealed class PushNotificationTokenHasherTests
{
    [Fact]
    public void HashIsStableDifferentAndDoesNotContainOriginalToken()
    {
        var hasher = new PushNotificationTokenHasher();
        string first = hasher.Hash("token-one-abcdefghijklmnopqrstuvwxyz");
        string second = hasher.Hash("token-one-abcdefghijklmnopqrstuvwxyz");
        string other = hasher.Hash("token-two-abcdefghijklmnopqrstuvwxyz");

        first.Should().Be(second).And.NotBe(other).And.NotBeNullOrWhiteSpace();
        first.Should().NotContain("token-one-abcdefghijklmnopqrstuvwxyz");
    }
}
