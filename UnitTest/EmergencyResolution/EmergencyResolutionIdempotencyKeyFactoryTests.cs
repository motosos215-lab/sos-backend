using FluentAssertions;
using MotoSOS.API.Modules.EmergencyResolution.Application;

namespace UnitTest.EmergencyResolution;

public sealed class EmergencyResolutionIdempotencyKeyFactoryTests
{
    [Fact]
    public void CreateReturnsStableKeyForUserAndIncident()
    {
        var factory = new EmergencyResolutionIdempotencyKeyFactory();

        string first = factory.Create("user", "incident");
        string second = factory.Create("user", "incident");

        first.Should().Be(second).And.HaveLength(64);
        first.Should().NotBe(factory.Create("user", "other"));
    }
}
