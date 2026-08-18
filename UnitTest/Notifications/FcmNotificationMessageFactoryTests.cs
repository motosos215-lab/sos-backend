using FluentAssertions;
using Microsoft.Extensions.Options;
using MotoSOS.API.Modules.Notifications.Domain;
using MotoSOS.API.Modules.Notifications.Providers;

namespace UnitTest.Notifications;

public sealed class FcmNotificationMessageFactoryTests
{
    [Fact]
    public void NormalAttemptWithoutEventTypeKeepsExistingSosPayload()
    {
        FcmPushRequest push = Factory().Create(new NotificationProviderRequest("attempt", "alert", "incident", NotificationProviderChannel.Push, false, RecipientUserId: "rider"), "token");

        push.Data.Should().ContainKey("channel");
        push.Data["notificationDeliveryAttemptId"].Should().Be("attempt");
        push.Data.Should().NotContainKey("eventType");
        push.Data.Should().NotContainKey("monitorAlertAttemptId");
    }

    [Fact]
    public void FeedbackAttemptWithEventTypeUsesRiderFeedbackPayloadIds()
    {
        DateTimeOffset occurredAt = new(2026, 8, 18, 10, 30, 0, TimeSpan.Zero);

        FcmPushRequest push = Factory().Create(new NotificationProviderRequest("feedback-attempt", "alert", "incident", NotificationProviderChannel.Push, false, RecipientUserId: "rider", EventType: NotificationEventTypes.MonitorAlertAcknowledged, MonitorAlertAttemptId: "monitor-attempt", MonitorUserId: "monitor", Screen: NotificationScreens.EmergencyStatus, OccurredAtUtc: occurredAt), "token");

        push.Data.Should().NotContainKey("channel");
        push.Data["eventType"].Should().Be(NotificationEventTypes.MonitorAlertAcknowledged);
        push.Data["notificationDeliveryAttemptId"].Should().Be("feedback-attempt");
        push.Data["monitorAlertAttemptId"].Should().Be("monitor-attempt");
        push.Data["monitorUserId"].Should().Be("monitor");
        push.Data["occurredAtUtc"].Should().Be(occurredAt.ToString("O"));
        push.Data["screen"].Should().Be(NotificationScreens.EmergencyStatus);
    }

    private static FcmNotificationMessageFactory Factory() => new(Options.Create(new FcmNotificationProviderOptions { DefaultTitle = "MotoSOS" }));
}
