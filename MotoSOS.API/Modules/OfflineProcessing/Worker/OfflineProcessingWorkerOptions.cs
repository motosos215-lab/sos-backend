namespace MotoSOS.API.Modules.OfflineProcessing.Worker;

public sealed class OfflineProcessingWorkerOptions
{
    public const string SectionName = "Mobile:OfflineProcessingWorker";

    public bool Enabled { get; set; }
    public int IntervalSeconds { get; set; } = 60;
    public int MaxItemsPerRun { get; set; } = 20;
    public bool RunOnStartup { get; set; }
    public int RecoveryMinutes { get; set; } = 10;
}
