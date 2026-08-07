using FluentAssertions;
using MotoSOS.API.Modules.OfflineProcessing.Application;
using MotoSOS.API.Modules.OfflineProcessing.Contracts;

namespace UnitTest.OfflineProcessing;

public sealed class OfflineProcessingValidatorTests
{
    [Fact]
    public void MaxItemsValidationAllowsDefaultAndBounds()
    {
        var validator = new RunOfflineProcessingRequestValidator();
        validator.Validate(new RunOfflineProcessingRequest(null)).IsValid.Should().BeTrue();
        validator.Validate(new RunOfflineProcessingRequest(1)).IsValid.Should().BeTrue();
        validator.Validate(new RunOfflineProcessingRequest(100)).IsValid.Should().BeTrue();
        validator.Validate(new RunOfflineProcessingRequest(0)).IsValid.Should().BeFalse();
        validator.Validate(new RunOfflineProcessingRequest(101)).IsValid.Should().BeFalse();
    }
}
