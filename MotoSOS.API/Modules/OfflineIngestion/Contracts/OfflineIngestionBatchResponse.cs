namespace MotoSOS.API.Modules.OfflineIngestion.Contracts;

public sealed record OfflineIngestionBatchResponse(
    string BatchId,
    DateTimeOffset ReceivedAtUtc,
    IReadOnlyList<OfflineIngestionItemResultResponse> Results);
