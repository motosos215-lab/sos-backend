using FluentAssertions;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Contracts;

namespace UnitTest.Trips;

public sealed class TripValidatorTests
{
    [Fact]
    public void StartTripRequiresVehicleIdAndMobileDeviceId()
    {
        var validator = new StartTripRequestValidator();

        validator.Validate(new StartTripRequest(null, null, null, null, null, null, null)).IsValid.Should().BeFalse();
        validator.Validate(new StartTripRequest("vehicle", "mobile", null, null, null, null, null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void StartTripValidatesLocationBatteryAndAppVersion()
    {
        var validator = new StartTripRequestValidator();

        validator.Validate(new StartTripRequest("vehicle", "mobile", null, null, new TripLocationRequest(-91, 0, 1, "gps", null), null, null)).IsValid.Should().BeFalse();
        validator.Validate(new StartTripRequest("vehicle", "mobile", null, null, new TripLocationRequest(0, -181, 1, "gps", null), null, null)).IsValid.Should().BeFalse();
        validator.Validate(new StartTripRequest("vehicle", "mobile", null, null, new TripLocationRequest(0, 0, -1, "gps", null), null, null)).IsValid.Should().BeFalse();
        validator.Validate(new StartTripRequest("vehicle", "mobile", null, null, null, 101, null)).IsValid.Should().BeFalse();
        validator.Validate(new StartTripRequest("vehicle", "mobile", null, null, null, 50, new string('a', 51))).IsValid.Should().BeFalse();
    }

    [Fact]
    public void FinishTripAllowsEmptyRequestAndValidatesBatteryNotesAndLocation()
    {
        var validator = new FinishTripRequestValidator();

        validator.Validate(new FinishTripRequest(null, null, null, null)).IsValid.Should().BeTrue();
        validator.Validate(new FinishTripRequest(null, null, -1, null)).IsValid.Should().BeFalse();
        validator.Validate(new FinishTripRequest(null, null, 50, new string('a', 501))).IsValid.Should().BeFalse();
        validator.Validate(new FinishTripRequest(null, new TripLocationRequest(91, 0, null, null, null), null, null)).IsValid.Should().BeFalse();
    }
}
