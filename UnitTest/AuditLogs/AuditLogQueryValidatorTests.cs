using FluentAssertions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Domain;

namespace UnitTest.AuditLogs;

public sealed class AuditLogQueryValidatorTests
{
    private readonly AuditLogQueryValidator _validator = new();

    [Fact]
    public void EmptyQueryIsValid()
    {
        AuditLogQuery query = _validator.Validate(null, null, null, null, null, null, null, null, null, null);
        query.PageNumber.Should().Be(1);
        query.PageSize.Should().Be(20);
    }

    [Fact]
    public void DateFromLessOrEqualDateToIsValid()
    {
        DateTimeOffset from = DateTimeOffset.Parse("2026-08-01T00:00:00Z");
        DateTimeOffset to = DateTimeOffset.Parse("2026-08-02T00:00:00Z");
        AuditLogQuery query = _validator.Validate(null, null, null, null, null, null, from, to, null, null);
        query.DateFrom.Should().Be(from);
        query.DateTo.Should().Be(to);
    }

    [Fact]
    public void DateFromGreaterThanDateToThrowsValidationError()
    {
        Action act = () => _validator.Validate(null, null, null, null, null, null, DateTimeOffset.Parse("2026-08-03T00:00:00Z"), DateTimeOffset.Parse("2026-08-02T00:00:00Z"), null, null);
        act.Should().Throw<ValidationAppException>().Where(e => e.Code == "validation_error");
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void InvalidPagingThrowsValidationError(int pageNumber, int pageSize)
    {
        Action act = () => _validator.Validate(null, null, null, null, null, null, null, null, pageNumber, pageSize);
        act.Should().Throw<ValidationAppException>().Where(e => e.Code == "validation_error");
    }

    [Fact]
    public void ValidEnumsAreParsed()
    {
        AuditLogQuery query = _validator.Validate(null, "IncidentClosed", "Incidents", "Success", null, null, null, null, null, null);
        query.Action.Should().Be(AuditAction.IncidentClosed);
        query.Module.Should().Be(AuditModule.Incidents);
        query.Outcome.Should().Be(AuditOutcome.Success);
    }

    [Theory]
    [InlineData("bad", null, null)]
    [InlineData(null, "bad", null)]
    [InlineData(null, null, "bad")]
    public void InvalidEnumsThrowValidationError(string? action, string? module, string? outcome)
    {
        Action act = () => _validator.Validate(null, action, module, outcome, null, null, null, null, null, null);
        act.Should().Throw<ValidationAppException>().Where(e => e.Code == "validation_error");
    }
}
