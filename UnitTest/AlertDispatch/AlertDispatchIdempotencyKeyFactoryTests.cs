using FluentAssertions;
using MotoSOS.API.Modules.AlertDispatch.Application;

namespace UnitTest.AlertDispatch;

public sealed class AlertDispatchIdempotencyKeyFactoryTests
{
    [Fact]
    public void SameInputProducesSameKeyAndDifferentPartsChangeIt()
    {
        var factory = new AlertDispatchIdempotencyKeyFactory();
        string clientId = Guid.NewGuid().ToString();

        string key = factory.Create("user", "incident", clientId);

        factory.Create("user", "incident", clientId).Should().Be(key);
        factory.Create("other", "incident", clientId).Should().NotBe(key);
        factory.Create("user", "other", clientId).Should().NotBe(key);
        factory.Create("user", "incident", Guid.NewGuid().ToString()).Should().NotBe(key);
        key.Should().NotContain("user").And.NotContain("incident").And.NotContain(clientId);
    }
}
