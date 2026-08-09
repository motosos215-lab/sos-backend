using FluentAssertions;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Contracts;

namespace UnitTest.Notifications;

public sealed class NotificationValidatorTests
{
    [Fact]
    public void ValidatorsEnforceRequiredFieldsAndLengths()
    {
        new PrepareNotificationAttemptsRequestValidator().Validate(new PrepareNotificationAttemptsRequest("alert", "ok")).IsValid.Should().BeTrue();
        new PrepareNotificationAttemptsRequestValidator().Validate(new PrepareNotificationAttemptsRequest("", null)).IsValid.Should().BeFalse();
        new PrepareNotificationAttemptsRequestValidator().Validate(new PrepareNotificationAttemptsRequest("alert", new string('a', 501))).IsValid.Should().BeFalse();
        new MarkNotificationSimulatedSentRequestValidator().Validate(new MarkNotificationSimulatedSentRequest(new string('a', 201), null)).IsValid.Should().BeFalse();
        new MarkNotificationFailedRequestValidator().Validate(new MarkNotificationFailedRequest(null, null)).IsValid.Should().BeFalse();
        new MarkNotificationFailedRequestValidator().Validate(new MarkNotificationFailedRequest(new string('a', 501), null)).IsValid.Should().BeFalse();
        new CancelNotificationAttemptRequestValidator().Validate(new CancelNotificationAttemptRequest(new string('a', 501), null)).IsValid.Should().BeFalse();
    }
}
