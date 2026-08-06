using FluentAssertions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;

namespace UnitTest.AlertAcknowledgements;

public sealed class AlertAcknowledgementIdempotencyKeyFactoryTests
{
    [Fact]
    public void SameInputProducesSameKeyAndDifferentPartsChangeIt()
    {
        var factory = new AlertAcknowledgementIdempotencyKeyFactory();
        string key = factory.Create("monitor", "attempt");
        factory.Create("monitor", "attempt").Should().Be(key);
        factory.Create("other", "attempt").Should().NotBe(key);
        factory.Create("monitor", "other").Should().NotBe(key);
        key.Should().NotContain("monitor").And.NotContain("attempt");
    }
}
