namespace MotoSOS.API.Modules.OfflineIngestion.Domain;

public enum OfflineIngestionProcessingStatus
{
    PendingProcessing = 1,
    Processed = 2,
    Ignored = 3,
    FailedPermanent = 4
}
