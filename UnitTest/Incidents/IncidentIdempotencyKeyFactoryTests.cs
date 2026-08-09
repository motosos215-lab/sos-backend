using FluentAssertions;
using MotoSOS.API.Modules.Incidents.Application;

namespace UnitTest.Incidents;

public sealed class IncidentIdempotencyKeyFactoryTests
{
    [Fact]
    public void SameInputProducesSameKeyAndScopeChangesKey()
    {
        var factory = new IncidentIdempotencyKeyFactory();

        string first = factory.Create("user", "trip", "client-incident");

        factory.Create("user", "trip", "client-incident").Should().Be(first);
        factory.Create("other", "trip", "client-incident").Should().NotBe(first);
        factory.Create("user", "other", "client-incident").Should().NotBe(first);
        factory.Create("user", "trip", "other").Should().NotBe(first);
        first.Should().NotContain("client-incident");
    }
}
