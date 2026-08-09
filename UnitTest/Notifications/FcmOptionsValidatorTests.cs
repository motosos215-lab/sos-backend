using FluentAssertions;
using MotoSOS.API.Modules.Notifications.Providers;

namespace UnitTest.Notifications;

public sealed class FcmOptionsValidatorTests
{
    [Fact]
    public void DisabledIsValidWithoutConfiguration()
    {
        FcmProviderConfigurationStatus status = new FcmNotificationProviderOptionsValidator().Validate(new FcmNotificationProviderOptions());

        status.Enabled.Should().BeFalse();
        status.Configured.Should().BeFalse();
        status.CredentialSource.Should().Be("none");
    }

    [Fact]
    public void EnabledRequiresProjectIdAndExactlyOneCredentialSource()
    {
        var validator = new FcmNotificationProviderOptionsValidator();

        validator.Validate(new FcmNotificationProviderOptions { Enabled = true }).Configured.Should().BeFalse();
        validator.Validate(new FcmNotificationProviderOptions { Enabled = true, ProjectId = "project", ServiceAccountJson = "{}" }).Configured.Should().BeTrue();
        validator.Validate(new FcmNotificationProviderOptions { Enabled = true, ProjectId = "project", ServiceAccountFilePath = "path" }).Configured.Should().BeTrue();
        validator.Validate(new FcmNotificationProviderOptions { Enabled = true, ProjectId = "project", ServiceAccountJson = "{}", ServiceAccountFilePath = "path" }).Configured.Should().BeFalse();
    }
}
