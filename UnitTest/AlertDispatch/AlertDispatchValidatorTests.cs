using FluentAssertions;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AlertDispatch.Contracts;

namespace UnitTest.AlertDispatch;

public sealed class AlertDispatchValidatorTests
{
    [Fact]
    public void CreateAlertDispatchValidatesRequiredFieldsAndRanges()
    {
        var validator = new CreateAlertDispatchRequestValidator();

        validator.Validate(Valid()).IsValid.Should().BeTrue();
        validator.Validate(Valid(incidentId: "")).IsValid.Should().BeFalse();
        validator.Validate(Valid(clientAlertRequestId: "bad")).IsValid.Should().BeFalse();
        validator.Validate(Valid(priority: "Bad")).IsValid.Should().BeFalse();
        validator.Validate(Valid(reason: "Bad")).IsValid.Should().BeFalse();
        validator.Validate(new CreateAlertDispatchRequest("incident", Guid.NewGuid().ToString(), "High", "IncidentCreated", null, "ok")).IsValid.Should().BeFalse();
        validator.Validate(Valid(notes: new string('a', 501))).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CancelAlertDispatchAllowsValidRequestAndLimitsReason()
    {
        var validator = new CancelAlertDispatchRequestValidator();

        validator.Validate(new CancelAlertDispatchRequest("ok", DateTimeOffset.UtcNow)).IsValid.Should().BeTrue();
        validator.Validate(new CancelAlertDispatchRequest(new string('a', 501), null)).IsValid.Should().BeFalse();
    }

    private static CreateAlertDispatchRequest Valid(string? incidentId = "incident", string? clientAlertRequestId = null, string? priority = "High", string? reason = "IncidentCreated", DateTimeOffset? requestedAtUtc = default, string? notes = "ok") =>
        new(incidentId, clientAlertRequestId ?? Guid.NewGuid().ToString(), priority, reason, requestedAtUtc ?? DateTimeOffset.UtcNow, notes);
}
