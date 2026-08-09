using FluentAssertions;
using MotoSOS.API.Modules.Escalations.Application;
using MotoSOS.API.Modules.Escalations.Contracts;

namespace UnitTest.Escalations;

public sealed class EmergencyEscalationValidatorTests
{
    private readonly CreateEmergencyEscalationRequestValidator _validator = new();

    [Fact]
    public void ValidRequestPasses() => _validator.Validate(new CreateEmergencyEscalationRequest("ManualEscalation", "Level1", "notes")).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null, "Level1")]
    [InlineData("bad", "Level1")]
    [InlineData("NoAcknowledgement", null)]
    [InlineData("NoAcknowledgement", "bad")]
    public void InvalidReasonOrLevelFails(string? reason, string? level) => _validator.Validate(new CreateEmergencyEscalationRequest(reason, level, null)).IsValid.Should().BeFalse();

    [Fact]
    public void NotesMaxLengthIsEnforced() => _validator.Validate(new CreateEmergencyEscalationRequest("ManualEscalation", "Level1", new string('a', 2001))).IsValid.Should().BeFalse();
}
