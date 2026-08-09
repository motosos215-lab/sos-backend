using FluentAssertions;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.Incidents.Contracts;

namespace UnitTest.Incidents;

public sealed class IncidentValidatorTests
{
    [Fact]
    public void CreateIncidentRequiresCoreFields()
    {
        var validator = new CreateIncidentRequestValidator();

        validator.Validate(new CreateIncidentRequest(null, null, null, null, null, null, null, null, null, null, null, null, null)).IsValid.Should().BeFalse();
        validator.Validate(Valid()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateIncidentValidatesRangesAndNestedObjects()
    {
        var validator = new CreateIncidentRequestValidator();

        validator.Validate(Valid(score: 101)).IsValid.Should().BeFalse();
        validator.Validate(Valid(confidence: 1.1)).IsValid.Should().BeFalse();
        validator.Validate(Valid(location: new IncidentLocationRequest(91, 0, null, null, null, null))).IsValid.Should().BeFalse();
        validator.Validate(Valid(location: new IncidentLocationRequest(0, -181, null, null, null, null))).IsValid.Should().BeFalse();
        validator.Validate(Valid(evidence: new IncidentEvidenceSummaryRequest(null, null, Enumerable.Range(0, 21).Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray(), null, null, null, null, null))).IsValid.Should().BeFalse();
        validator.Validate(Valid(evidence: new IncidentEvidenceSummaryRequest(null, null, null, null, null, 101, null, null))).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CancelAndCloseAllowValidRequests()
    {
        new CancelFalsePositiveRequestValidator().Validate(new CancelFalsePositiveRequest("Estoy bien", DateTimeOffset.UtcNow)).IsValid.Should().BeTrue();
        new CloseIncidentRequestValidator().Validate(new CloseIncidentRequest("Resolved", "Ok", DateTimeOffset.UtcNow)).IsValid.Should().BeTrue();
    }

    private static CreateIncidentRequest Valid(int? score = 87, double? confidence = 0.9, IncidentLocationRequest? location = null, IncidentEvidenceSummaryRequest? evidence = null) => new(
        "trip",
        Guid.NewGuid().ToString(),
        "MobileDetection",
        "CountdownTimeout",
        "High",
        score,
        confidence,
        "Good",
        "rules-v1",
        "validation-v1",
        DateTimeOffset.UtcNow,
        location,
        evidence);
}
