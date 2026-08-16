using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MotoSOS.API.Common.Exceptions;

namespace MotoSOS.API.Modules.EvidenceAttachments.Application;

public sealed partial class EvidenceFileValidator
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp", "application/pdf", "text/plain" };
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".pdf", ".txt" };

    public EvidenceFileValidationResult Validate(string? fileName, string? contentType, long sizeBytes, long maxFileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName)) throw new ValidationAppException("File is required.");
        if (sizeBytes <= 0) throw new ValidationAppException("File must not be empty.");
        if (sizeBytes > maxFileSizeBytes) throw new ValidationAppException("File exceeds the maximum allowed size.");

        string safeName = SanitizeFileName(fileName);
        string extension = Path.GetExtension(safeName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension)) throw new ValidationAppException("File extension is not allowed.");

        string normalizedContentType = NormalizeContentType(contentType);
        if (!AllowedContentTypes.Contains(normalizedContentType)) throw new ValidationAppException("File content type is not allowed.");
        if (!IsExtensionCompatible(normalizedContentType, extension)) throw new ValidationAppException("File extension does not match content type.");

        return new EvidenceFileValidationResult(safeName, normalizedContentType, extension);
    }

    public string CreateObjectKey(string basePath, string environmentName, string incidentId, string evidenceAttachmentId, string safeFileName)
    {
        string prefix = SanitizePathSegment(basePath, allowSlash: true);
        string env = SanitizePathSegment(environmentName, allowSlash: false);
        string incident = SanitizePathSegment(incidentId, allowSlash: false);
        string evidenceId = SanitizePathSegment(evidenceAttachmentId, allowSlash: false);
        string storedFileName = $"{Guid.NewGuid():N}-{safeFileName}";
        return string.Join('/', new[] { prefix, env, incident, evidenceId, storedFileName }.Where(v => !string.IsNullOrWhiteSpace(v)));
    }

    public static async Task<string> ComputeSha256Async(Stream content, CancellationToken cancellationToken)
    {
        content.Position = 0;
        byte[] hash = await SHA256.HashDataAsync(content, cancellationToken);
        content.Position = 0;
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool IsSafeClientEvidenceId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        string trimmed = value.Trim();
        return trimmed.Length <= 100 && SafeClientEvidenceIdRegex().IsMatch(trimmed) && !trimmed.Contains("..", StringComparison.Ordinal);
    }

    private static string NormalizeContentType(string? value) => value?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()?.ToLowerInvariant() ?? string.Empty;

    private static string SanitizeFileName(string fileName)
    {
        string trimmed = fileName.Trim();
        if (trimmed.Contains('/', StringComparison.Ordinal)
            || trimmed.Contains('\\', StringComparison.Ordinal)
            || trimmed.Contains("..", StringComparison.Ordinal)
            || Path.IsPathRooted(trimmed)) throw new ValidationAppException("File name must be safe.");

        string name = Path.GetFileName(trimmed);
        if (name.Length == 0 || name != trimmed) throw new ValidationAppException("File name must be safe.");
        StringBuilder safe = new(name.Length);
        foreach (char c in name)
        {
            safe.Append(char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '-');
        }

        string result = DuplicateDashRegex().Replace(safe.ToString(), "-").Trim('-', '.');
        if (string.IsNullOrWhiteSpace(result)) throw new ValidationAppException("File name must be safe.");
        return result.Length <= 120 ? result : result[^120..];
    }

    private static string SanitizePathSegment(string value, bool allowSlash)
    {
        string trimmed = value.Trim().Replace('\\', '/');
        if (trimmed.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(trimmed)) throw new ValidationAppException("Storage path is invalid.");
        string pattern = allowSlash ? "[^a-zA-Z0-9_./-]" : "[^a-zA-Z0-9_.-]";
        string sanitized = Regex.Replace(trimmed, pattern, "-").Trim('/', '-', '.');
        return DuplicateSlashRegex().Replace(sanitized, "/");
    }

    private static bool IsExtensionCompatible(string contentType, string extension) => contentType switch
    {
        "image/jpeg" => extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase),
        "image/png" => extension.Equals(".png", StringComparison.OrdinalIgnoreCase),
        "image/webp" => extension.Equals(".webp", StringComparison.OrdinalIgnoreCase),
        "application/pdf" => extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase),
        "text/plain" => extension.Equals(".txt", StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    [GeneratedRegex("-+")]
    private static partial Regex DuplicateDashRegex();

    [GeneratedRegex("/+")]
    private static partial Regex DuplicateSlashRegex();

    [GeneratedRegex("^[a-zA-Z0-9_.:-]+$")]
    private static partial Regex SafeClientEvidenceIdRegex();
}

public sealed record EvidenceFileValidationResult(string SafeFileName, string ContentType, string Extension);
