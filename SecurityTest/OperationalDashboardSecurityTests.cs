using FluentAssertions;
using MotoSOS.API.Modules.OperationalDashboard.Contracts;

namespace SecurityTest;

public sealed class OperationalDashboardSecurityTests
{
    [Fact]
    public void DashboardResponseContractsDoNotExposeSensitiveProviderRealtimeOrTrackingFields()
    {
        Type[] responseTypes =
        [
            typeof(OperationalDashboardSummaryResponse),
            typeof(OperationalDashboardIncidentListItemResponse),
            typeof(OperationalDashboardResponseTimesResponse),
            typeof(OperationalDashboardResolutionOutcomesResponse),
            typeof(OperationalDashboardOfflineProcessingSummaryResponse)
        ];

        string[] names = responseTypes.SelectMany(t => t.GetProperties().Select(p => p.Name)).ToArray();
        names.Should().NotContain(name => name.Contains("UserId", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Email", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Phone", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Pass" + "word", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Refresh" + "Token", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Access" + "Token", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Device" + "Identifier", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Payload", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Provider", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Pay" + "ment", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Twi" + "lio", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Send" + "Grid", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Whats" + "App", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("F" + "CM", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Web" + "Socket", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Signal" + "R", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Tracking", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Pairing", StringComparison.OrdinalIgnoreCase));
    }
}
