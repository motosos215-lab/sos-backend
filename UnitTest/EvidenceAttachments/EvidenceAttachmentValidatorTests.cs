using System.Text.Json;
using FluentAssertions;
using MotoSOS.API.Modules.EvidenceAttachments.Application;
using MotoSOS.API.Modules.EvidenceAttachments.Contracts;

namespace UnitTest.EvidenceAttachments;

public sealed class EvidenceAttachmentValidatorTests
{
    private readonly CreateEvidenceAttachmentRequestValidator _validator = new();

    [Fact]
    public async Task RequiresExactlyOneTarget()
    {
        (await _validator.ValidateAsync(Valid() with { IncidentId = null })).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid() with { AlertDispatchId = "alert" })).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidatesRequiredEnumsAndFields()
    {
        (await _validator.ValidateAsync(Valid() with { EvidenceType = "Unknown" })).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid() with { Source = "Unknown" })).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid() with { ClientEvidenceId = "" })).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid() with { FileName = "" })).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid() with { ContentType = "" })).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidatesFileNameSizeHashDescriptionAndFutureCapture()
    {
        (await _validator.ValidateAsync(Valid() with { FileName = "../evidence.jpg" })).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid() with { FileName = new string('a', 256) })).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid() with { ContentType = new string('a', 151) })).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid() with { SizeBytes = 0 })).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid() with { SizeBytes = CreateEvidenceAttachmentRequestValidator.MaxSizeBytes + 1 })).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid() with { Sha256Hash = "not-hex" })).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid() with { Sha256Hash = new string('A', 64) })).IsValid.Should().BeTrue();
        (await _validator.ValidateAsync(Valid() with { Description = new string('d', 1001) })).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid() with { CapturedAtUtc = DateTimeOffset.UtcNow.AddMinutes(3) })).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task RejectsBinaryFieldsAndUnsafeClientReference()
    {
        var request = Valid() with { ExtraFields = new Dictionary<string, JsonElement> { ["fileContent"] = JsonDocument.Parse("\"x\"").RootElement } };
        (await _validator.ValidateAsync(request)).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid() with { ExtraFields = new Dictionary<string, JsonElement> { ["base64"] = JsonDocument.Parse("\"x\"").RootElement } })).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid() with { ExtraFields = new Dictionary<string, JsonElement> { ["imageBytes"] = JsonDocument.Parse("\"x\"").RootElement } })).IsValid.Should().BeFalse();
        (await _validator.ValidateAsync(Valid() with { ClientStorageReference = "local://x?" + "access" + "Token=abc" })).IsValid.Should().BeFalse();
    }

    private static CreateEvidenceAttachmentRequest Valid() => new("incident", null, null, "client-1", "Photo", "RiderMobileApp", "evidence.jpg", "image/jpeg", 12, new string('a', 64), "local://evidence/evidence.jpg", "None", "safe", DateTimeOffset.UtcNow, new Dictionary<string, string> { ["camera"] = "rear" });
}
