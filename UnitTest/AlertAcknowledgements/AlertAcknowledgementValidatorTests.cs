using FluentAssertions;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertAcknowledgements.Contracts;

namespace UnitTest.AlertAcknowledgements;

public sealed class AlertAcknowledgementValidatorTests
{
    [Fact]
    public void AcknowledgeAndDeclineValidateResponseTypeAndMessageLength()
    {
        new AcknowledgeAlertRequestValidator().Validate(new AcknowledgeAlertRequest("CanAssist", "ok")).IsValid.Should().BeTrue();
        new AcknowledgeAlertRequestValidator().Validate(new AcknowledgeAlertRequest(null, null)).IsValid.Should().BeFalse();
        new AcknowledgeAlertRequestValidator().Validate(new AcknowledgeAlertRequest("Bad", null)).IsValid.Should().BeFalse();
        new AcknowledgeAlertRequestValidator().Validate(new AcknowledgeAlertRequest("CanAssist", new string('a', 501))).IsValid.Should().BeFalse();
        new DeclineAlertRequestValidator().Validate(new DeclineAlertRequest("CannotAssist", "ok")).IsValid.Should().BeTrue();
        new DeclineAlertRequestValidator().Validate(new DeclineAlertRequest(null, null)).IsValid.Should().BeFalse();
        new DeclineAlertRequestValidator().Validate(new DeclineAlertRequest("CannotAssist", new string('a', 501))).IsValid.Should().BeFalse();
    }
}
