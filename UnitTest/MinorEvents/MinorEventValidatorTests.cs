using FluentAssertions;
using MotoSOS.API.Modules.MinorEvents.Application;
using MotoSOS.API.Modules.MinorEvents.Contracts;

namespace UnitTest.MinorEvents;

public sealed class MinorEventValidatorTests
{
    private readonly CreateMinorEventRequestValidator _validator = new();

    [Fact]
    public void ValidRequestPasses() => _validator.Validate(Request()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null, "client", "HardBrake", "Low", "MobileApp")]
    [InlineData("trip", null, "HardBrake", "Low", "MobileApp")]
    [InlineData("trip", "client", null, "Low", "MobileApp")]
    [InlineData("trip", "client", "Bad", "Low", "MobileApp")]
    [InlineData("trip", "client", "HardBrake", null, "MobileApp")]
    [InlineData("trip", "client", "HardBrake", "Bad", "MobileApp")]
    [InlineData("trip", "client", "HardBrake", "Low", null)]
    [InlineData("trip", "client", "HardBrake", "Low", "Bad")]
    public void RequiredEnumsAreValidated(string? tripId, string? clientEventId, string? eventType, string? severity, string? source) => _validator.Validate(Request(tripId: tripId, clientEventId: clientEventId, eventType: eventType, severity: severity, source: source)).IsValid.Should().BeFalse();

    [Theory]
    [InlineData(-0.1, 42, 19, -99)]
    [InlineData(1.1, 42, 19, -99)]
    [InlineData(0.7, -1, 19, -99)]
    [InlineData(0.7, 101, 19, -99)]
    [InlineData(0.7, 42, -91, -99)]
    [InlineData(0.7, 42, 91, -99)]
    [InlineData(0.7, 42, 19, -181)]
    [InlineData(0.7, 42, 19, 181)]
    public void NumericBoundsAreValidated(double confidence, double score, double latitude, double longitude) => _validator.Validate(Request(confidence: confidence, score: score, latitude: latitude, longitude: longitude)).IsValid.Should().BeFalse();

    [Fact]
    public void FutureOccurredAtAndMessageLengthAreValidated()
    {
        _validator.Validate(Request(occurredAtUtc: DateTimeOffset.UtcNow.AddMinutes(3))).IsValid.Should().BeFalse();
        _validator.Validate(Request(message: new string('a', 1001))).IsValid.Should().BeFalse();
    }

    private static CreateMinorEventRequest Request(string? tripId = "trip", string? clientEventId = "client", string? eventType = "HardBrake", string? severity = "Low", string? source = "MobileApp", double? confidence = 0.72, double? score = 42, double? latitude = 19.2826, double? longitude = -99.6557, DateTimeOffset? occurredAtUtc = null, string? message = "message") => new(tripId, clientEventId, eventType, severity, source, null, null, score, confidence, "Good", latitude, longitude, 45.2, 82, message, occurredAtUtc ?? DateTimeOffset.UtcNow, null);
}
