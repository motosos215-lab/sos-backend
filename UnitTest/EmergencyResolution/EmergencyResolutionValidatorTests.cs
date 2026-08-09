using FluentAssertions;
using MotoSOS.API.Modules.EmergencyResolution.Application;
using MotoSOS.API.Modules.EmergencyResolution.Contracts;

namespace UnitTest.EmergencyResolution;

public sealed class EmergencyResolutionValidatorTests
{
    [Fact]
    public async Task ValidatorRequiresValidOutcomeAndSummary()
    {
        var validator = new CreateEmergencyResolutionReportRequestValidator();

        (await validator.ValidateAsync(new CreateEmergencyResolutionReportRequest("Unknown", "summary", null))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new CreateEmergencyResolutionReportRequest("RealEmergency", null, null))).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(new CreateEmergencyResolutionReportRequest("FalsePositive", "Resolved safely", "notes"))).IsValid.Should().BeTrue();
    }
}
