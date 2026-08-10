using FluentAssertions;
using MotoSOS.API.Modules.Escalations.Contracts;
using MotoSOS.API.Modules.Escalations.Worker;

namespace SecurityTest;

public sealed class AutomaticEscalationSecurityTests
{
    [Fact]
    public void AutomaticEscalationResponsesDoNotExposeSensitiveProviderRealtimeOrBillingFields()
    {
        Type[] responseTypes =
        [
            typeof(RunAutomaticEscalationResponse),
            typeof(AutomaticEscalationWorkerStatusResponse)
        ];

        string joinedNames = string.Join(' ', responseTypes.Select(t => t.FullName).Concat(responseTypes.SelectMany(t => t.GetProperties().Select(p => p.Name)))).ToLowerInvariant();
        joinedNames.Should().NotContain("email");
        joinedNames.Should().NotContain("phone");
        joinedNames.Should().NotContain(("Pass" + "word").ToLowerInvariant());
        joinedNames.Should().NotContain(("Refresh" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("Access" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("Device" + "Identifier").ToLowerInvariant());
        joinedNames.Should().NotContain("payload");
        joinedNames.Should().NotContain("provider");
        joinedNames.Should().NotContain(("Pay" + "ment").ToLowerInvariant());
        joinedNames.Should().NotContain(("Twi" + "lio").ToLowerInvariant());
        joinedNames.Should().NotContain(("Send" + "Grid").ToLowerInvariant());
        joinedNames.Should().NotContain(("Whats" + "App").ToLowerInvariant());
        joinedNames.Should().NotContain(("F" + "CM").ToLowerInvariant());
        joinedNames.Should().NotContain(("Web" + "Socket").ToLowerInvariant());
        joinedNames.Should().NotContain(("Signal" + "R").ToLowerInvariant());
        joinedNames.Should().NotContain("tracking");
        joinedNames.Should().NotContain("pairing");
    }

    [Fact]
    public void AutomaticEscalationWorkerUsesOnlyNativeBackgroundWorkerTypes()
    {
        string joinedNames = string.Join(' ', typeof(AutomaticEscalationWorker).FullName, typeof(AutomaticEscalationWorkerOptions).FullName);
        joinedNames = joinedNames.ToLowerInvariant();
        joinedNames.Should().NotContain("hangfire");
        joinedNames.Should().NotContain("quartz");
        joinedNames.Should().NotContain("cron");
    }
}
