namespace MotoSOS.API.Modules.OfflineIngestion.Contracts;

public sealed record OfflineIngestionItemResultResponse(
    string ClientEventId,
    string Type,
    string Status,
    string AckId,
    string RemoteRecordId,
    bool IsDuplicate);
