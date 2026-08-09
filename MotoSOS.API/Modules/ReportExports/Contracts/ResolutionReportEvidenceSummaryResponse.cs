namespace MotoSOS.API.Modules.ReportExports.Contracts;

public sealed record ResolutionReportEvidenceSummaryResponse(string EvidenceAttachmentId, string TargetType, string EvidenceType, string Source, string Status, string FileName, string ContentType, long SizeBytes, string? Sha256Hash, string RegisteredByRole, DateTimeOffset CapturedAtUtc, DateTimeOffset CreatedAtUtc);
