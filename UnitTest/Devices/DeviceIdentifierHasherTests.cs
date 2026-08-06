using FluentAssertions;
using MotoSOS.API.Modules.Devices.Application;

namespace UnitTest.Devices;

public sealed class DeviceIdentifierHasherTests
{
    [Fact]
    public void SameInputProducesSameHash()
    {
        var hasher = new DeviceIdentifierHasher();

        hasher.Hash("local-device-id").Should().Be(hasher.Hash("local-device-id"));
    }

    [Fact]
    public void DifferentInputsProduceDifferentHashesAndOriginalIsNotReturned()
    {
        var hasher = new DeviceIdentifierHasher();

        string? hash = hasher.Hash("local-device-id");

        hash.Should().NotBe("local-device-id");
        hash.Should().NotBe(hasher.Hash("other-device-id"));
        hasher.Hash(null).Should().BeNull();
        hasher.Hash("   ").Should().BeNull();
    }
}
