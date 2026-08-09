using System.Globalization;
using FluentAssertions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.OperationalDashboard.Application;

namespace UnitTest.OperationalDashboard;

public sealed class OperationalDashboardQueryValidatorTests
{
    private readonly OperationalDashboardQueryValidator _validator = new();

    [Fact]
    public void EmptyAndOrderedDateRangesAreValid()
    {
        _validator.ValidateDateRange(null, null).Should().NotBeNull();
        _validator.ValidateDateRange(DateTimeOffset.Parse("2026-08-01T00:00:00Z", CultureInfo.InvariantCulture), DateTimeOffset.Parse("2026-08-02T00:00:00Z", CultureInfo.InvariantCulture)).Should().NotBeNull();
    }

    [Fact]
    public void DateFromGreaterThanDateToThrowsValidationError()
    {
        Action act = () => _validator.ValidateDateRange(DateTimeOffset.Parse("2026-08-03T00:00:00Z", CultureInfo.InvariantCulture), DateTimeOffset.Parse("2026-08-02T00:00:00Z", CultureInfo.InvariantCulture));
        act.Should().Throw<ValidationAppException>().Which.Code.Should().Be("validation_error");
    }

    [Theory]
    [InlineData(0, 20, null)]
    [InlineData(1, 0, null)]
    [InlineData(1, 101, null)]
    [InlineData(1, 20, "closed")]
    [InlineData(1, 20, "Invalid")]
    public void InvalidPagingOrStatusThrowsValidationError(int pageNumber, int pageSize, string? status)
    {
        Action act = () => _validator.ValidateIncidentQuery(null, null, status, pageNumber, pageSize);
        act.Should().Throw<ValidationAppException>().Which.Code.Should().Be("validation_error");
    }

    [Fact]
    public void ValidIncidentQueryAppliesDefaultsAndParsesStatusExactly()
    {
        OperationalDashboardQuery query = _validator.ValidateIncidentQuery(null, null, "Closed", null, null);
        query.PageNumber.Should().Be(1);
        query.PageSize.Should().Be(20);
        query.ParsedStatus.Should().NotBeNull();
    }
}
