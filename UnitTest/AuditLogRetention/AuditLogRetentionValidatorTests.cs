using FluentAssertions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogRetention.Application;
using MotoSOS.API.Modules.AuditLogRetention.Contracts;

namespace UnitTest.AuditLogRetention;

public sealed class AuditLogRetentionValidatorTests
{
    [Fact]
    public void NullRequestUsesSafeDefaults()
    {
        ValidatedAuditLogRetentionRunRequest request = new RunAuditLogRetentionRequestValidator().Validate(null);

        request.RetentionDays.Should().Be(180);
        request.DryRun.Should().BeTrue();
        request.ConfirmPermanentDelete.Should().BeFalse();
    }

    [Theory]
    [InlineData(89)]
    [InlineData(3651)]
    public void RetentionDaysOutsidePolicyThrowsValidationError(int retentionDays)
    {
        Action act = () => new RunAuditLogRetentionRequestValidator().Validate(new RunAuditLogRetentionRequest(retentionDays, true, false));

        act.Should().Throw<ValidationAppException>();
    }

    [Fact]
    public void DeleteWithoutExplicitConfirmationThrowsValidationError()
    {
        Action act = () => new RunAuditLogRetentionRequestValidator().Validate(new RunAuditLogRetentionRequest(180, false, false));

        act.Should().Throw<ValidationAppException>();
    }

    [Fact]
    public void DeleteWithExplicitConfirmationIsValid()
    {
        ValidatedAuditLogRetentionRunRequest request = new RunAuditLogRetentionRequestValidator().Validate(new RunAuditLogRetentionRequest(180, false, true));

        request.DryRun.Should().BeFalse();
        request.ConfirmPermanentDelete.Should().BeTrue();
    }
}
