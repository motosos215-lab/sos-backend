using FluentAssertions;
using MotoSOS.API.Modules.EmergencyResolution.Contracts;

namespace SecurityTest;

public sealed class EmergencyResolutionSecurityTests
{
    [Fact]
    public void EmergencyResolutionResponseDoesNotExposeSecretsProvidersOrLiveTrackingFields()
    {
        string[] names = typeof(EmergencyResolutionReportResponse).GetProperties().Select(p => p.Name).ToArray();

        names.Should().NotContain(name => name.Contains("Pass" + "word", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Refresh" + "Token", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Access" + "Token", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Device" + "Identifier", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Provider", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Pay" + "ment", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Str" + "ipe", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Google" + "Play", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Twi" + "lio", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Send" + "Grid", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Whats" + "App", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("F" + "CM", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Web" + "Socket", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Signal" + "R", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Tracking", StringComparison.OrdinalIgnoreCase));
    }
}
