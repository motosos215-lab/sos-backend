namespace MotoSOS.API.Modules.OfflineIngestion.Contracts;

public sealed record OfflineIngestionBatchRequest(
    string? BatchId,
    string? MobileDeviceId,
    string? TripId,
    int? SchemaVersion,
    DateTimeOffset? SentAtUtc,
    string? AppVersion,
    IReadOnlyList<OfflineIngestionItemRequest>? Items);
