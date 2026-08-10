using FluentAssertions;
using MotoSOS.API.Modules.MinorEvents.Application;

namespace UnitTest.MinorEvents;

public sealed class MinorEventIdempotencyKeyFactoryTests
{
    [Fact]
    public void SameInputProducesSameKeyAndDifferentInputChangesKey()
    {
        var factory = new MinorEventIdempotencyKeyFactory();
        string key = factory.Create("user", "trip", "client", "HardBrake");
        factory.Create("user", "trip", "client", "HardBrake").Should().Be(key);
        factory.Create("other", "trip", "client", "HardBrake").Should().NotBe(key);
        factory.Create("user", "other", "client", "HardBrake").Should().NotBe(key);
        factory.Create("user", "trip", "other", "HardBrake").Should().NotBe(key);
        factory.Create("user", "trip", "client", "SharpTurn").Should().NotBe(key);
        key.Should().NotContain("user").And.NotContain("trip").And.NotContain("client");
    }
}
