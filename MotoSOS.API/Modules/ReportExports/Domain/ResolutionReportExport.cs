using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MotoSOS.API.Modules.Users.Domain;

namespace MotoSOS.API.Modules.ReportExports.Domain;

public sealed class ResolutionReportExport
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();
    public string UserId { get; set; } = string.Empty;
    public string IncidentId { get; set; } = string.Empty;
    public string EmergencyResolutionReportId { get; set; } = string.Empty;
    public ResolutionReportExportType ExportType { get; set; } = ResolutionReportExportType.Json;
    public ResolutionReportExportStatus Status { get; set; } = ResolutionReportExportStatus.Generated;
    public string RequestedByUserId { get; set; } = string.Empty;
    public UserRole RequestedByRole { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}
