using FluentAssertions;
using MotoSOS.API.Modules.ReportExports.Contracts;
using MotoSOS.API.Modules.ReportExports.Domain;

namespace SecurityTest;

public sealed class ResolutionReportExportSecurityTests
{
    [Fact]
    public void ExportContractsDoNotExposeSensitiveOrBinaryFields()
    {
        Type[] types = [typeof(ResolutionReportExportResponse), typeof(ResolutionReportEvidenceSummaryResponse), typeof(ResolutionReportAuditSummaryResponse), typeof(ResolutionReportExport)];
        string joinedNames = string.Join(' ', types.Select(t => t.FullName).Concat(types.SelectMany(t => t.GetProperties().Select(p => p.Name)))).ToLowerInvariant();

        joinedNames.Should().NotContain(("pass" + "word").ToLowerInvariant());
        joinedNames.Should().NotContain(("refresh" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("access" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("device" + "Identifier").ToLowerInvariant());
        joinedNames.Should().NotContain(("provider" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("pay" + "load").ToLowerInvariant());
        joinedNames.Should().NotContain("filecontent");
        joinedNames.Should().NotContain("imagebytes");
        joinedNames.Should().NotContain("videobytes");
        joinedNames.Should().NotContain("audiobytes");
        joinedNames.Should().NotContain("base64");
        joinedNames.Should().NotContain("email");
        joinedNames.Should().NotContain("phone");
        joinedNames.Should().NotContain("metadata");
        joinedNames.Should().NotContain("storageobjectkey");
        joinedNames.Should().NotContain("clientstoragereference");
        joinedNames.Should().NotContain("polyline");
        joinedNames.Should().NotContain("tracking");
        joinedNames.Should().NotContain("route");
    }

    [Fact]
    public void OnlyJsonExportTypeExists()
    {
        Enum.GetNames<ResolutionReportExportType>().Should().Equal("Json");
    }
}
