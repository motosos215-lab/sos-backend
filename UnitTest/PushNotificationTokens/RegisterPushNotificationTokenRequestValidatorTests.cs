using FluentAssertions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.PushNotificationTokens.Application;
using MotoSOS.API.Modules.PushNotificationTokens.Contracts;

namespace UnitTest.PushNotificationTokens;

public sealed class RegisterPushNotificationTokenRequestValidatorTests
{
    private readonly RegisterPushNotificationTokenRequestValidator _validator = new();

    [Theory]
    [InlineData(null, "Fcm", Token)]
    [InlineData("Android", null, Token)]
    [InlineData("Android", "Fcm", null)]
    [InlineData("Bad", "Fcm", Token)]
    [InlineData("Android", "Bad", Token)]
    public void RequiredAndEnumRulesAreValidated(string? platform, string? channel, string? token)
    {
        Action act = () => _validator.Validate(new RegisterPushNotificationTokenRequest(platform, channel, null, token, null));

        act.Should().Throw<ValidationAppException>();
    }

    [Theory]
    [InlineData("Android", "Fcm")]
    [InlineData("Ios", "Apns")]
    [InlineData("Web", "WebPush")]
    [InlineData("Web", "Fcm")]
    public void SupportedCombinationsAreValid(string platform, string channel)
    {
        ValidatedRegisterPushNotificationTokenRequest request = _validator.Validate(new RegisterPushNotificationTokenRequest(platform, channel, "device", Token, null));

        request.DeviceId.Should().Be("device");
    }

    [Theory]
    [InlineData("Android", "Apns")]
    [InlineData("Ios", "Fcm")]
    [InlineData("Ios", "WebPush")]
    public void UnsupportedCombinationThrowsValidationError(string platform, string channel)
    {
        Action act = () => _validator.Validate(new RegisterPushNotificationTokenRequest(platform, channel, null, Token, null));

        act.Should().Throw<ValidationAppException>();
    }

    [Fact]
    public void TokenAndDeviceLengthAreValidated()
    {
        _validator.Invoking(v => v.Validate(new RegisterPushNotificationTokenRequest("Android", "Fcm", null, new string('a', 19), null))).Should().Throw<ValidationAppException>();
        _validator.Invoking(v => v.Validate(new RegisterPushNotificationTokenRequest("Android", "Fcm", null, new string('a', 4097), null))).Should().Throw<ValidationAppException>();
        _validator.Invoking(v => v.Validate(new RegisterPushNotificationTokenRequest("Android", "Fcm", new string('d', 101), Token, null))).Should().Throw<ValidationAppException>();
    }

    [Theory]
    [InlineData("userId")]
    [InlineData("tokenHash")]
    [InlineData("status")]
    [InlineData("providerCredentials")]
    public void ForbiddenBodyPropertiesThrowValidationError(string propertyName)
    {
        var request = new RegisterPushNotificationTokenRequest("Android", "Fcm", null, Token, null) { ExtraProperties = new Dictionary<string, System.Text.Json.JsonElement> { [propertyName] = default } };

        Action act = () => _validator.Validate(request);

        act.Should().Throw<ValidationAppException>();
    }

    [Fact]
    public void MetadataIsSanitizedAndTruncated()
    {
        ValidatedRegisterPushNotificationTokenRequest request = _validator.Validate(new RegisterPushNotificationTokenRequest("Android", "Fcm", null, Token, new Dictionary<string, string> { ["appVersion"] = new string('x', 250), ["refresh" + "Token"] = "hidden", ["fileContent"] = "hidden" }));

        request.Metadata.Should().ContainKey("appVersion");
        request.Metadata["appVersion"].Should().HaveLength(200);
        request.Metadata.Should().NotContainKey("refresh" + "Token");
        request.Metadata.Should().NotContainKey("fileContent");
    }

    private const string Token = "abcdefghijklmnopqrstuvwxyz1234567890";
}
