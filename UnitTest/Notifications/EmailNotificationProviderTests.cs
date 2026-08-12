using FluentAssertions;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.Notifications.Providers;

namespace UnitTest.Notifications;

public sealed class EmailNotificationProviderTests
{
    [Theory]
    [InlineData("from")]
    [InlineData("host")]
    [InlineData("username")]
    [InlineData("password")]
    public async Task MissingConfigurationFailsControlledAndDoesNotCallSender(string missing)
    {
        EmailNotificationProviderOptions options = Configured();
        if (missing == "from") options.FromEmail = string.Empty;
        if (missing == "host") options.SmtpHost = string.Empty;
        if (missing == "username") options.SmtpUsername = string.Empty;
        if (missing == "password") options.SmtpPassword = string.Empty;
        var sender = new Sender();
        EmailNotificationProvider provider = Create(sender, options);

        NotificationProviderResult result = await provider.SendAsync(Request(), CancellationToken.None);

        result.DeliveryStatus.Should().Be(NotificationProviderDeliveryStatus.Failed);
        result.ErrorCode.Should().Be("provider_not_configured");
        sender.Calls.Should().Be(0);
    }

    [Fact]
    public async Task SuccessfulSendReturnsSafeSentResultAndMinimalBody()
    {
        var sender = new Sender("smtp-message-id");
        EmailNotificationProvider provider = Create(sender, Configured());

        NotificationProviderResult result = await provider.SendAsync(Request(), CancellationToken.None);

        result.ProviderType.Should().Be(NotificationProviderType.Email);
        result.DeliveryStatus.Should().Be(NotificationProviderDeliveryStatus.Sent);
        result.ProviderMessageId.Should().Be("smtp-message-id");
        sender.LastMessage!.ToEmail.Should().Be("monitor@example.com");
        sender.LastMessage.Subject.Should().Be("MotoSOS - Alerta de emergencia");
        sender.LastMessage.Body.Should().Contain("incidentId: incident").And.Contain("alertDispatchId: alert").And.Contain("notificationDeliveryAttemptId: attempt").And.NotContain("token");
    }

    [Fact]
    public async Task MissingRecipientOrSenderFailureFailsControlled()
    {
        EmailNotificationProvider missingRecipient = Create(new Sender(), Configured());
        EmailNotificationProvider failingSender = Create(new Sender(null, fail: true), Configured());

        NotificationProviderResult missingRecipientResult = await missingRecipient.SendAsync(Request(null), CancellationToken.None);
        NotificationProviderResult senderFailureResult = await failingSender.SendAsync(Request(), CancellationToken.None);

        missingRecipientResult.ErrorCode.Should().Be("email_recipient_not_available");
        senderFailureResult.ErrorCode.Should().Be("email_provider_failed");
        senderFailureResult.ErrorMessage.Should().NotContain("smtp-secret");
    }

    private static NotificationProviderRequest Request(string? email = "monitor@example.com") => new("attempt", "alert", "incident", NotificationProviderChannel.Email, false, email);
    private static EmailNotificationProviderOptions Configured() => new() { Enabled = true, FromEmail = "alerts@example.com", FromName = "MotoSOS", SmtpHost = "smtp.example.test", SmtpPort = 587, SmtpUsername = "smtp-user", SmtpPassword = "smtp-secret", UseSsl = true };
    private static EmailNotificationProvider Create(IEmailNotificationSender sender, EmailNotificationProviderOptions options) => new(sender, new EmailNotificationProviderOptionsValidator(), Options.Create(options), new Clock());
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero); }
    private sealed class Sender(string? messageId = null, bool fail = false) : IEmailNotificationSender { public int Calls { get; private set; } public EmailNotificationMessage? LastMessage { get; private set; } public Task<string?> SendAsync(EmailNotificationMessage message, EmailNotificationProviderOptions options, CancellationToken cancellationToken) { Calls++; LastMessage = message; if (fail) throw new InvalidOperationException("smtp-secret must not leak"); return Task.FromResult(messageId); } }
}
