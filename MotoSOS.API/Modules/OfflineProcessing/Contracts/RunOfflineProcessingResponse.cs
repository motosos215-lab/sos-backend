namespace MotoSOS.API.Modules.OfflineProcessing.Contracts;

public sealed record RunOfflineProcessingResponse(int Processed, int Skipped, int Failed, IReadOnlyList<OfflineProcessingItemResultResponse> Items);
