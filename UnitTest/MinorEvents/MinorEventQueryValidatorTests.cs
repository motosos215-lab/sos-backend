using FluentAssertions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.MinorEvents.Application;
using MotoSOS.API.Modules.MinorEvents.Domain;

namespace UnitTest.MinorEvents;

public sealed class MinorEventQueryValidatorTests
{
    private readonly MinorEventQueryValidator _validator = new();

    [Fact]
    public void ValidQueryParsesEnumsAndDefaults()
    {
        MinorEventQuery query = _validator.Validate("user", "trip", "HardBrake", "Low", "Recorded", null, null, null, null);
        query.UserId.Should().Be("user"); query.TripId.Should().Be("trip"); query.EventType.Should().Be(MinorEventType.HardBrake); query.Severity.Should().Be(MinorEventSeverity.Low); query.Status.Should().Be(MinorEventStatus.Recorded); query.PageNumber.Should().Be(1); query.PageSize.Should().Be(20);
    }

    [Theory]
    [InlineData("Bad", "Low", "Recorded")]
    [InlineData("HardBrake", "Bad", "Recorded")]
    [InlineData("HardBrake", "Low", "Bad")]
    public void InvalidEnumsThrowValidationError(string eventType, string severity, string status) => Assert.Throws<ValidationAppException>(() => _validator.Validate(null, null, eventType, severity, status, null, null, null, null));

    [Fact]
    public void InvalidPagingOrDateRangeThrowsValidationError()
    {
        Assert.Throws<ValidationAppException>(() => _validator.Validate(null, null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(-1), null, null));
        Assert.Throws<ValidationAppException>(() => _validator.Validate(null, null, null, null, null, null, null, 0, null));
        Assert.Throws<ValidationAppException>(() => _validator.Validate(null, null, null, null, null, null, null, null, 101));
    }
}
