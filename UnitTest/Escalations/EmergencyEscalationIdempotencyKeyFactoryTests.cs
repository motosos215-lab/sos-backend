using FluentAssertions;
using MotoSOS.API.Modules.Escalations.Application;

namespace UnitTest.Escalations;

public sealed class EmergencyEscalationIdempotencyKeyFactoryTests
{
    [Fact]
    public void SameInputProducesSameKeyAndDifferentInputChangesKey()
    {
        var factory = new EmergencyEscalationIdempotencyKeyFactory();
        string key = factory.Create("user", "dispatch");
        factory.Create("user", "dispatch").Should().Be(key);
        factory.Create("other", "dispatch").Should().NotBe(key);
        factory.Create("user", "other").Should().NotBe(key);
        key.Should().NotContain("user").And.NotContain("dispatch");
    }
}
