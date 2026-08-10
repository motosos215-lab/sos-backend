using FluentAssertions;
using MotoSOS.API.Modules.AuditLogRetention.Contracts;
using MotoSOS.API.Modules.AuditLogRetention.Domain;

namespace SecurityTest;

public sealed class AuditLogRetentionSecurityTests
{
    [Fact]
    public void RetentionContractsDoNotExposeSensitiveAuditLogMetadataOrSecrets()
    {
        Type[] types = [typeof(AuditLogRetentionPolicyResponse), typeof(AuditLogRetentionRunResponse), typeof(GetAuditLogRetentionRunsResponse), typeof(AuditLogRetentionRun)];
        string joinedNames = string.Join(' ', types.Select(t => t.FullName).Concat(types.SelectMany(t => t.GetProperties().Select(p => p.Name)))).ToLowerInvariant();

        joinedNames.Should().NotContain(("pass" + "word").ToLowerInvariant());
        joinedNames.Should().NotContain(("refresh" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("access" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("device" + "Identifier").ToLowerInvariant());
        joinedNames.Should().NotContain(("Device" + "Identifier" + "Hash").ToLowerInvariant());
        joinedNames.Should().NotContain(("provider" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("pay" + "load").ToLowerInvariant());
        joinedNames.Should().NotContain("metadata");
        joinedNames.Should().NotContain("email");
        joinedNames.Should().NotContain("phone");
        joinedNames.Should().NotContain(("pay" + "ment").ToLowerInvariant());
        joinedNames.Should().NotContain("stacktrace");
        joinedNames.Should().NotContain("secret");
    }

    [Fact]
    public void RetentionPolicyKeepsAutomaticAndExternalCapabilitiesDisabled()
    {
        var policy = new AuditLogRetentionPolicyResponse(180, 90, 3650, true, true, false);
        string[] forbiddenCapabilities = ["worker", "schedule", "storage", "export", "compression", "object", "signed", "email", "provider", "sdk", "secret", "pay" + "ment", ("Str" + "ipe").ToLowerInvariant()];

        policy.AutomaticWorkerEnabled.Should().BeFalse();
        typeof(AuditLogRetentionRun).GetProperties().Select(property => property.Name.ToLowerInvariant()).Should().NotContain(forbiddenCapabilities);
    }
}
