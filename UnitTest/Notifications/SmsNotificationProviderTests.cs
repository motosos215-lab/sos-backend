using FluentAssertions;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.Notifications.Providers;

namespace UnitTest.Notifications;

public sealed class SmsNotificationProviderTests
{
    [Theory]
    [InlineData("apiKey")]
    [InlineData("sender")]
    [InlineData("provider")]
    [InlineData("timeout")]
    public async Task MissingOrInvalidConfigurationFailsControlledAndDoesNotCallSender(string missing)
    {
        SmsNotificationProviderOptions options = Configured();
        if (missing == "apiKey") options.ApiKey = string.Empty;
        if (missing == "sender") options.Sender = string.Empty;
        if (missing == "provider") options.Provider = "Unsupported";
        if (missing == "timeout") options.TimeoutSeconds = 0;
        var sender = new Sender();
        SmsNotificationProvider provider = Create(sender, options);

        NotificationProviderResult result = await provider.SendAsync(Request(), CancellationToken.None);

        result.DeliveryStatus.Should().Be(NotificationProviderDeliveryStatus.Failed);
        result.ErrorCode.Should().Be("provider_not_configured");
        sender.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData("+52 55 1234 5678", "+52", "+525512345678")]
    [InlineData("55-1234-5678", "+52", "+525512345678")]
    [InlineData("(55) 1234-5678", "+52", "+525512345678")]
    public void NormalizesPhoneNumbers(string input, string countryCode, string expected)
    {
        SmsNotificationProvider.TryNormalizePhoneNumber(input, countryCode, out string? normalized).Should().BeTrue();
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("5512345678")]
    public void RejectsInvalidPhoneNumbers(string? input)
    {
        SmsNotificationProvider.TryNormalizePhoneNumber(input, null, out string? normalized).Should().BeFalse();
        normalized.Should().BeNull();
    }

    [Fact]
    public async Task SuccessfulSendReturnsSafeSentResultAndShortBody()
    {
        var sender = new Sender("sms-message-id");
        SmsNotificationProvider provider = Create(sender, Configured());

        NotificationProviderResult result = await provider.SendAsync(Request(), CancellationToken.None);

        result.ProviderType.Should().Be(NotificationProviderType.Sms);
        result.DeliveryStatus.Should().Be(NotificationProviderDeliveryStatus.Sent);
        result.ProviderMessageId.Should().Be("sms-message-id");
        sender.LastMessage!.ToPhoneNumber.Should().Be("+525512345678");
        sender.LastMessage.Body.Should().Contain("MotoSOS: alerta de emergencia detectada").And.Contain("ID: attempt").And.NotContain("token");
        sender.LastMessage.Body.Length.Should().BeLessThan(160);
    }

    [Fact]
    public async Task SenderFailureFailsControlledWithoutSensitiveValues()
    {
        SmsNotificationProvider provider = Create(new Sender(null, fail: true), Configured());

        NotificationProviderResult result = await provider.SendAsync(Request(), CancellationToken.None);

        result.DeliveryStatus.Should().Be(NotificationProviderDeliveryStatus.Failed);
        result.ErrorCode.Should().Be("sms_provider_failed");
        result.ErrorMessage.Should().NotContain("sms-api-key").And.NotContain("+525512345678");
    }

    private static NotificationProviderRequest Request(string? phone = "55 1234 5678") => new("attempt", "alert", "incident", NotificationProviderChannel.Sms, false, null, phone);
    private static SmsNotificationProviderOptions Configured() => new() { Enabled = true, Provider = "Brevo", ApiKey = "sms-api-key", Sender = "MotoSOS", DefaultCountryCode = "+52", TimeoutSeconds = 15 };
    private static SmsNotificationProvider Create(ISmsNotificationSender sender, SmsNotificationProviderOptions options) => new(sender, new SmsNotificationProviderOptionsValidator(), Options.Create(options), new Clock());
    private sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero); }
    private sealed class Sender(string? messageId = null, bool fail = false) : ISmsNotificationSender { public int Calls { get; private set; } public SmsNotificationMessage? LastMessage { get; private set; } public Task<string?> SendAsync(SmsNotificationMessage message, SmsNotificationProviderOptions options, CancellationToken cancellationToken) { Calls++; LastMessage = message; if (fail) throw new InvalidOperationException("sms-api-key +525512345678 must not leak"); return Task.FromResult(messageId); } }
}
