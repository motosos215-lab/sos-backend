using System.Text.Json;

namespace MotoSOS.API.Modules.OfflineIngestion.Contracts;

public sealed record OfflineIngestionItemRequest(
    string? ClientEventId,
    string? Type,
    DateTimeOffset? OccurredAtUtc,
    int? PayloadVersion,
    JsonElement Payload);
