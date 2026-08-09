namespace MotoSOS.API.Modules.OfflineProcessing.Contracts;

public sealed record OfflineProcessingItemResultResponse(string OfflineRecordId, string Type, string Status, string? RemoteRecordId, string? Reason = null, string? ErrorCode = null);
