using FluentAssertions;
using MotoSOS.API.Modules.OfflineIngestion.Application;

namespace UnitTest.OfflineIngestion;

public sealed class OfflineIngestionIdempotencyKeyFactoryTests
{
    [Fact]
    public void SameInputProducesSameKeyAndDifferentScopeChangesKey()
    {
        var factory = new OfflineIngestionIdempotencyKeyFactory();

        string first = factory.Create("user", "mobile", "trip", "minor-event", "event", 1);
        string same = factory.Create("user", "mobile", "trip", "minor-event", "event", 1);

        same.Should().Be(first);
        factory.Create("other", "mobile", "trip", "minor-event", "event", 1).Should().NotBe(first);
        factory.Create("user", "other", "trip", "minor-event", "event", 1).Should().NotBe(first);
        factory.Create("user", "mobile", "other", "minor-event", "event", 1).Should().NotBe(first);
        factory.Create("user", "mobile", "trip", "minor-event", "other", 1).Should().NotBe(first);
        factory.Create("user", "mobile", "trip", "minor-event", "event", 2).Should().NotBe(first);
        first.Should().NotContain("event");
    }
}
