using System.Text.RegularExpressions;
using FluentValidation;
using MotoSOS.API.Modules.EvidenceAttachments.Contracts;
using MotoSOS.API.Modules.EvidenceAttachments.Domain;

namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public sealed partial class CreateEvidenceAttachmentRequestValidator : AbstractValidator<CreateEvidenceAttachmentRequest>
{
    public const long MaxSizeBytes = 50L * 1024L * 1024L;
    private static readonly string[] ForbiddenExtraFields = ["base64", "fileContent", "imageBytes", "videoBytes", "audioBytes"];
    private static readonly string[] SensitiveReferenceParts = ["pass" + "word", "access" + "Token", "refresh" + "Token", "tok" + "en", "authorization", "bearer", "credential", "signature", "sig=", "key="];

    public CreateEvidenceAttachmentRequestValidator()
    {
        RuleFor(x => x).Must(HaveExactlyOneTarget).WithMessage("Exactly one evidence target is required.");
        RuleFor(x => x).Must(NotContainForbiddenExtraFields).WithMessage("Binary evidence fields are not accepted.");
        RuleFor(x => x.EvidenceType).NotEmpty().Must(v => Enum.TryParse(v, false, out EvidenceType parsed) && parsed != EvidenceType.Unknown).WithMessage("EvidenceType is invalid.");
        RuleFor(x => x.Source).NotEmpty().Must(v => Enum.TryParse(v, false, out EvidenceSource parsed) && parsed != EvidenceSource.Unknown).WithMessage("Source is invalid.");
        RuleFor(x => x.ClientEvidenceId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255).Must(BeSafeFileName).WithMessage("FileName must be a safe file name without path segments.");
        RuleFor(x => x.ContentType).NotEmpty().MaximumLength(150);
        RuleFor(x => x.SizeBytes).NotNull().InclusiveBetween(1, MaxSizeBytes);
        RuleFor(x => x.Sha256Hash).Must(v => string.IsNullOrWhiteSpace(v) || Sha256HexRegex().IsMatch(v)).WithMessage("Sha256Hash must be a 64 character hex value.");
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.CapturedAtUtc).NotNull().Must(v => !v.HasValue || v.Value.ToUniversalTime() <= DateTimeOffset.UtcNow.AddMinutes(2)).WithMessage("CapturedAtUtc cannot be more than 2 minutes in the future.");
        RuleFor(x => x.ClientStorageReference).MaximumLength(500).Must(v => string.IsNullOrWhiteSpace(v) || !SensitiveReferenceParts.Any(part => v.Contains(part, StringComparison.OrdinalIgnoreCase))).WithMessage("ClientStorageReference contains unsafe data.");
        RuleFor(x => x.StorageProvider).Must(v => string.IsNullOrWhiteSpace(v) || Enum.TryParse(v, false, out EvidenceStorageProvider _)).WithMessage("StorageProvider is invalid.");
    }

    private static bool HaveExactlyOneTarget(CreateEvidenceAttachmentRequest request)
    {
        int targets = Count(request.IncidentId) + Count(request.AlertDispatchId) + Count(request.EmergencyResolutionReportId);
        return targets == 1;
    }

    private static bool NotContainForbiddenExtraFields(CreateEvidenceAttachmentRequest request) => request.ExtraFields is null || !request.ExtraFields.Keys.Any(key => ForbiddenExtraFields.Any(forbidden => string.Equals(key, forbidden, StringComparison.OrdinalIgnoreCase)));

    private static bool BeSafeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string trimmed = value.Trim();
        if (trimmed != value) return false;
        if (trimmed.Contains("..", StringComparison.Ordinal) || trimmed.Contains('/', StringComparison.Ordinal) || trimmed.Contains('\\', StringComparison.Ordinal)) return false;
        if (Path.IsPathRooted(trimmed)) return false;
        return !trimmed.Any(char.IsControl);
    }

    private static int Count(string? value) => string.IsNullOrWhiteSpace(value) ? 0 : 1;

    [GeneratedRegex("^[a-fA-F0-9]{64}$")]
    private static partial Regex Sha256HexRegex();
}
