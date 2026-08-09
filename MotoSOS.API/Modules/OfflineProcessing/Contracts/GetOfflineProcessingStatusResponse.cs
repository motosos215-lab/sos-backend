namespace MotoSOS.API.Modules.OfflineProcessing.Contracts;

public sealed record GetOfflineProcessingStatusResponse(long Pending, long Processing, long Processed, long Failed, long Skipped);
