using FluentAssertions;
using MotoSOS.API.Modules.ReportExports.Application;

namespace UnitTest.ReportExports;

public sealed class ResolutionReportExportIdempotencyKeyFactoryTests
{
    private readonly ResolutionReportExportIdempotencyKeyFactory _factory = new();

    [Fact]
    public void SameInputProducesSameKeyAndDifferentPartsChangeIt()
    {
        string key = _factory.Create("user", "report", "Json");
        key.Should().Be(_factory.Create("user", "report", "Json"));
        key.Should().NotBe(_factory.Create("other", "report", "Json"));
        key.Should().NotBe(_factory.Create("user", "other", "Json"));
        key.Should().NotBe(_factory.Create("user", "report", "Other"));
        key.Should().NotContain("user").And.NotContain("report").And.HaveLength(64);
    }
}
