using FluentAssertions;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.AuditLogs.Contracts;
using MotoSOS.API.Modules.AuditLogs.Domain;

namespace SecurityTest;

public sealed class AuditLogSecurityTests
{
    [Fact]
    public void AuditLogContractsDoNotExposeSensitiveProviderRealtimeOrTrackingFields()
    {
        Type[] responseTypes = [typeof(AuditLogResponse), typeof(GetAuditLogsResponse), typeof(AuditLogMetadataItemResponse)];
        string[] names = responseTypes.SelectMany(t => t.GetProperties().Select(p => p.Name)).ToArray();
        names.Should().NotContain(name => name.Contains("Pass" + "wordHash", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Refresh" + "Token", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Access" + "Token", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Device" + "Identifier", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Provider" + "Token", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Payload", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Pay" + "ment", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Twi" + "lio", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Send" + "Grid", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Whats" + "App", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("F" + "CM", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Web" + "Socket", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Signal" + "R", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Tracking", StringComparison.OrdinalIgnoreCase));
        names.Should().NotContain(name => name.Contains("Pairing", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MetadataSanitizerRemovesSensitiveKeysCaseInsensitiveAndTruncatesValues()
    {
        Dictionary<string, string> sanitized = AuditLogService.SanitizeMetadata(new Dictionary<string, string> { ["Password"] = "secret", ["refreshTOKEN"] = "secret", ["DeviceIdentifierHash"] = "secret", ["providerToken"] = "secret", ["Payload"] = "{}", ["emailAddress"] = "user@example.com", ["phoneNumber"] = "+5255", ["cardNumber"] = "4111", ["mongoError"] = "internal", ["safeCount"] = "1", ["safeLong"] = new string('x', 300) });
        sanitized.Keys.Should().OnlyContain(key => key == "safeCount" || key == "safeLong");
        sanitized["safeLong"].Length.Should().Be(AuditLogService.MetadataValueMaxLength);
    }

    [Fact]
    public void AuditEnumsDoNotIntroduceExternalProvidersRealtimePaymentsOrPairing()
    {
        string allNames = string.Join(' ', Enum.GetNames<AuditAction>().Concat(Enum.GetNames<AuditModule>()).Concat(Enum.GetNames<AuditOutcome>()));
        allNames.Should().NotContain("Twilio").And.NotContain("SendGrid").And.NotContain("WhatsApp").And.NotContain("FCM").And.NotContain("WebSocket").And.NotContain("SignalR").And.NotContain("Tracking").And.NotContain("Stripe").And.NotContain("GooglePlay").And.NotContain("Pairing");
    }
}
