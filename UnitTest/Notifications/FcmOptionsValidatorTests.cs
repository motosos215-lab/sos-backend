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
        validator.Validate(new FcmNotificationProviderOptions { Enabled = true, ProjectId = "project", ServiceAccountJsonBase64 = Base64Json }).Configured.Should().BeTrue();
        validator.Validate(new FcmNotificationProviderOptions { Enabled = true, ProjectId = "project", ServiceAccountFilePath = "path" }).Configured.Should().BeTrue();
        validator.Validate(new FcmNotificationProviderOptions { Enabled = true, ProjectId = "project", ServiceAccountJson = "{}", ServiceAccountFilePath = "path" }).Configured.Should().BeTrue();
    }

    [Fact]
    public void CredentialSourceUsesPriorityAndReportsBase64Safely()
    {
        var validator = new FcmNotificationProviderOptionsValidator();

        validator.Validate(new FcmNotificationProviderOptions { Enabled = true, ProjectId = "project", ServiceAccountJsonBase64 = Base64Json }).CredentialSource.Should().Be("environment_json_base64");
        validator.Validate(new FcmNotificationProviderOptions { Enabled = true, ProjectId = "project", ServiceAccountJson = "{}", ServiceAccountJsonBase64 = Base64Json, ServiceAccountFilePath = "path" }).CredentialSource.Should().Be("environment_json");
        validator.Validate(new FcmNotificationProviderOptions { Enabled = true, ProjectId = "project", ServiceAccountJsonBase64 = Base64Json, ServiceAccountFilePath = "path" }).CredentialSource.Should().Be("environment_json_base64");
        validator.Validate(new FcmNotificationProviderOptions { Enabled = true, ProjectId = "project", ServiceAccountJsonBase64 = Base64Json, ServiceAccountFilePath = "path" }).Warnings.Should().Contain("multiple_credential_sources_configured");
    }

    private static string Base64Json => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{}"));
}
