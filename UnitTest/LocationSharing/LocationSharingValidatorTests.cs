using FluentAssertions;
using MotoSOS.API.Modules.LocationSharing.Application;
using MotoSOS.API.Modules.LocationSharing.Contracts;

namespace UnitTest.LocationSharing;

public sealed class LocationSharingValidatorTests
{
    [Fact]
    public void ShareLocationValidatesRequiredFieldsAndRanges()
    {
        var validator = new ShareLocationSnapshotRequestValidator();
        validator.Validate(Valid()).IsValid.Should().BeTrue();
        validator.Validate(Valid(incidentId: "")).IsValid.Should().BeFalse();
        validator.Validate(Valid(clientId: "bad")).IsValid.Should().BeFalse();
        validator.Validate(Valid(latitude: 91)).IsValid.Should().BeFalse();
        validator.Validate(Valid(longitude: 181)).IsValid.Should().BeFalse();
        validator.Validate(Valid(accuracy: -1)).IsValid.Should().BeFalse();
        validator.Validate(Valid(speed: -1)).IsValid.Should().BeFalse();
        validator.Validate(Valid(heading: 361)).IsValid.Should().BeFalse();
        validator.Validate(Valid(battery: 101)).IsValid.Should().BeFalse();
        validator.Validate(Valid(source: "Bad")).IsValid.Should().BeFalse();
        validator.Validate(new ShareLocationSnapshotRequest("incident", Guid.NewGuid().ToString(), 1, 1, null, null, null, null, null, "MobileApp", null)).IsValid.Should().BeFalse();
    }

    private static ShareLocationSnapshotRequest Valid(string? incidentId = "incident", string? clientId = null, double? latitude = 1, double? longitude = 1, double? accuracy = null, double? speed = null, double? heading = null, int? battery = null, string? source = "MobileApp") => new(incidentId, clientId ?? Guid.NewGuid().ToString(), latitude, longitude, accuracy, null, speed, heading, battery, source, DateTimeOffset.UtcNow);
}
