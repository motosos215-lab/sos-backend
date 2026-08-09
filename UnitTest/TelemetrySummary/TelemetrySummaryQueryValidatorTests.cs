using FluentAssertions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.TelemetrySummary.Application;

namespace UnitTest.TelemetrySummary;

public sealed class TelemetrySummaryQueryValidatorTests
{
    [Fact]
    public void EmptyQueryIsValid()
    {
        TelemetrySummaryQuery query = new TelemetrySummaryQueryValidator().Validate(null, null, null, null, null, null, null, null);
        query.PageNumber.Should().Be(1);
        query.PageSize.Should().Be(20);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void InvalidPagingThrowsValidationError(int pageNumber, int pageSize)
    {
        Action act = () => new TelemetrySummaryQueryValidator().Validate(null, null, null, null, null, null, pageNumber, pageSize);
        act.Should().Throw<ValidationAppException>();
    }

    [Fact]
    public void DateFromGreaterThanDateToThrowsValidationError()
    {
        Action act = () => new TelemetrySummaryQueryValidator().Validate(null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(-1), null, null);
        act.Should().Throw<ValidationAppException>();
    }

    [Theory]
    [InlineData("BadTripStatus", null)]
    [InlineData(null, "BadSummaryStatus")]
    public void InvalidEnumsThrowValidationError(string? tripStatus, string? summaryStatus)
    {
        Action act = () => new TelemetrySummaryQueryValidator().Validate(null, null, tripStatus, summaryStatus, null, null, null, null);
        act.Should().Throw<ValidationAppException>();
    }
}
