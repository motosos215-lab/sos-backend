using FluentAssertions;
using MotoSOS.API.Modules.EvidenceAttachments.Contracts;
using MotoSOS.API.Modules.EvidenceAttachments.Domain;

namespace SecurityTest;

public sealed class EvidenceAttachmentSecurityTests
{
    [Fact]
    public void EvidenceAttachmentResponsesDoNotExposeSensitiveOrBinaryFields()
    {
        Type[] responseTypes = [typeof(EvidenceAttachmentResponse), typeof(CreateEvidenceAttachmentResponse), typeof(GetEvidenceAttachmentsResponse), typeof(EvidenceAttachment)];
        string joinedNames = string.Join(' ', responseTypes.Select(t => t.FullName).Concat(responseTypes.SelectMany(t => t.GetProperties().Select(p => p.Name)))).ToLowerInvariant();

        joinedNames.Should().NotContain(("pass" + "wordHash").ToLowerInvariant());
        joinedNames.Should().NotContain(("pass" + "word").ToLowerInvariant());
        joinedNames.Should().NotContain(("refresh" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("access" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("device" + "Identifier").ToLowerInvariant());
        joinedNames.Should().NotContain(("device" + "IdentifierHash").ToLowerInvariant());
        joinedNames.Should().NotContain(("provider" + "Token").ToLowerInvariant());
        joinedNames.Should().NotContain(("pay" + "load").ToLowerInvariant());
        joinedNames.Should().NotContain("filebytes");
        joinedNames.Should().NotContain("imagebytes");
        joinedNames.Should().NotContain("videobytes");
        joinedNames.Should().NotContain("audiobytes");
        joinedNames.Should().NotContain("base64");
        joinedNames.Should().NotContain("email");
        joinedNames.Should().NotContain("phone");
        joinedNames.Should().NotContain(("pay" + "ment").ToLowerInvariant());
        joinedNames.Should().NotContain("stacktrace");
        joinedNames.Should().NotContain(("connection" + "String").ToLowerInvariant());
        joinedNames.Should().NotContain("secret");
        joinedNames.Should().NotContain("polyline");
        joinedNames.Should().NotContain("tracking");
        joinedNames.Should().NotContain("route");
    }

    [Fact]
    public void EvidenceAttachmentsDoNotExposeStorageSecretsOrUnsupportedFeatures()
    {
        Type[] types = [typeof(CreateEvidenceAttachmentRequest), typeof(EvidenceAttachmentResponse), typeof(UploadEvidenceAttachmentResponse), typeof(EvidenceStorageProvider)];
        string joinedNames = string.Join(' ', types.Select(t => t.FullName).Concat(types.SelectMany(t => t.GetProperties().Select(p => p.Name))).Concat(Enum.GetNames<EvidenceStorageProvider>())).ToLowerInvariant();

        joinedNames.Should().NotContain("gridfs");
        joinedNames.Should().NotContain("signedurl");
        joinedNames.Should().NotContain("accesskey");
        joinedNames.Should().NotContain("secretkey");
        joinedNames.Should().NotContain("bucket");
        joinedNames.Should().NotContain("objectkey");
        joinedNames.Should().NotContain(("web" + "socket").ToLowerInvariant());
        joinedNames.Should().NotContain(("signal" + "r").ToLowerInvariant());
        joinedNames.Should().NotContain("ocr");
        joinedNames.Should().NotContain("prediction");
        joinedNames.Should().NotContain("pairing");
    }
}
