using System.Text.Json;
using System.Text.Json.Serialization;

namespace MotoSOS.API.Modules.EvidenceAttachments.Contracts;

public sealed record CreateEvidenceAttachmentRequest(
    string? IncidentId,
    string? AlertDispatchId,
    string? EmergencyResolutionReportId,
    string? ClientEvidenceId,
    string? EvidenceType,
    string? Source,
    string? FileName,
    string? ContentType,
    long? SizeBytes,
    string? Sha256Hash,
    string? ClientStorageReference,
    string? StorageProvider,
    string? Description,
    DateTimeOffset? CapturedAtUtc,
    IReadOnlyDictionary<string, string>? Metadata)
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraFields { get; init; }
}
