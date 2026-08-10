using FluentAssertions;
using Microsoft.Extensions.Options;
using MotoSOS.API.Modules.Notifications.Providers;

namespace UnitTest.Notifications;

public sealed class FirebaseFcmPushClientTests
{
    [Fact]
    public async Task InvalidServiceAccountJsonBase64FailsControlledWithoutExposingCredentialValue()
    {
        const string invalidBase64 = "not-valid-base64";
        var options = new FcmNotificationProviderOptions { Enabled = true, ProjectId = "project", ServiceAccountJsonBase64 = invalidBase64 };
        var client = new FirebaseFcmPushClient(Options.Create(options), new FcmNotificationProviderOptionsValidator());

        FcmPushResult result = await client.SendAsync(new FcmPushRequest("token", "title", "body", new Dictionary<string, string>(), 60), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.FailureCode.Should().Be("fcm_credentials_invalid");
        result.FailureMessage.Should().NotContain(invalidBase64);
    }
}
