using FluentAssertions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.ReportExports.Application;
using MotoSOS.API.Modules.ReportExports.Domain;

namespace UnitTest.ReportExports;

public sealed class ResolutionReportExportQueryValidatorTests
{
    private readonly ResolutionReportExportQueryValidator _validator = new();

    [Fact]
    public void EmptyQueryIsValidAndJsonIsDefaultExportType()
    {
        ResolutionReportExportQuery query = _validator.Validate(null, null, null, null, null, null, null, null, null);
        query.PageNumber.Should().Be(1);
        query.PageSize.Should().Be(20);
        _validator.ValidateExportType(null).Should().Be(ResolutionReportExportType.Json);
    }

    [Fact]
    public void OnlyJsonExportTypeIsAccepted()
    {
        _validator.ValidateExportType("Json").Should().Be(ResolutionReportExportType.Json);
        Assert.Throws<ValidationAppException>(() => _validator.ValidateExportType("PDF"));
        Assert.Throws<ValidationAppException>(() => _validator.ValidateExportType("FuturePdf"));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void InvalidPaginationThrows(int pageNumber, int pageSize) => Assert.Throws<ValidationAppException>(() => _validator.Validate(null, null, null, null, null, null, null, pageNumber, pageSize));

    [Fact]
    public void ValidatesDatesAndStatus()
    {
        _validator.Validate(null, null, null, "Json", "Generated", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, 1, 20).Status.Should().Be(ResolutionReportExportStatus.Generated);
        Assert.Throws<ValidationAppException>(() => _validator.Validate(null, null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(-1), 1, 20));
        Assert.Throws<ValidationAppException>(() => _validator.Validate(null, null, null, null, "Nope", null, null, 1, 20));
    }
}
