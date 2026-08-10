using FluentAssertions;
using MotoSOS.API.Modules.TelemetrySummary.Contracts;
using MotoSOS.API.Modules.TelemetrySummary.Domain;

namespace SecurityTest;

public sealed class TelemetrySummarySecurityTests
{
    [Fact]
    public void TelemetrySummaryResponsesDoNotExposeSensitiveLocationRouteRealtimeOrBillingFields()
    {
        Type[] responseTypes =
        [
            typeof(TelemetrySummaryResponse),
            typeof(GetTelemetrySummariesResponse),
            typeof(TripTelemetrySummary)
        ];

        string joinedNames = string.Join(' ', responseTypes.Select(t => t.FullName).Concat(responseTypes.SelectMany(t => t.GetProperties().Select(p => p.Name)))).ToLowerInvariant();
        joinedNames.Should().NotContain("latitude");
        joinedNames.Should().NotContain("longitude");
        joinedNames.Should().NotContain("coordinate");
        joinedNames.Should().NotContain("route");
        joinedNames.Should().NotContain("polyline");
        joinedNames.Should().NotContain("tracking");
        joinedNames.Should().NotContain("metadata");
        joinedNames.Should().NotContain("message");
        joinedNames.Should().NotContain("payload");
        joinedNames.Should().NotContain("eventslist");
        joinedNames.Should().NotContain("email");
        joinedNames.Should().NotContain("phone");
        joinedNames.Should().NotContain(("Pass" + "word").ToLowerInvariant());
        joinedNames.Should().NotContain(("Refresh" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("Access" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("Device" + "Identifier").ToLowerInvariant());
        joinedNames.Should().NotContain("provider");
        joinedNames.Should().NotContain(("Pay" + "ment").ToLowerInvariant());
        joinedNames.Should().NotContain(("Web" + "Socket").ToLowerInvariant());
        joinedNames.Should().NotContain("prediction");
        joinedNames.Should().NotContain("riskScore".ToLowerInvariant());
        joinedNames.Should().NotContain("pairing");
    }
}
