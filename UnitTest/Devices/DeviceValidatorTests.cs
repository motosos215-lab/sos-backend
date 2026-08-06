using FluentAssertions;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Contracts;

namespace UnitTest.Devices;

public sealed class DeviceValidatorTests
{
    [Fact]
    public void LinkMobileRequiresCodeDeviceNameAndValidPlatform()
    {
        var validator = new LinkMobileDeviceRequestValidator();

        validator.Validate(new LinkMobileDeviceRequest(null, null, "Windows", null, null, null, null, null)).IsValid.Should().BeFalse();
        validator.Validate(new LinkMobileDeviceRequest("MSOS-8X7Q-3M2K", "Motorola Edge", "Android", null, null, null, null, null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SmartwatchBatteryMustBeInRange()
    {
        var validator = new LinkSmartwatchRequestValidator();

        validator.Validate(new LinkSmartwatchRequest("mobile-id", "Galaxy Watch", "WearOS", null, null, null, null, null, 101)).IsValid.Should().BeFalse();
        validator.Validate(new LinkSmartwatchRequest("mobile-id", "Galaxy Watch", "WearOS", null, null, null, null, null, 80)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void HeartbeatAllowsValidPayloadAndRejectsInvalidBattery()
    {
        var validator = new HeartbeatDeviceRequestValidator();

        validator.Validate(new HeartbeatDeviceRequest(87, "Online", "1.0.0")).IsValid.Should().BeTrue();
        validator.Validate(new HeartbeatDeviceRequest(-1, "Online", "1.0.0")).IsValid.Should().BeFalse();
        validator.Validate(new HeartbeatDeviceRequest(50, "Busy", null)).IsValid.Should().BeFalse();
    }
}
