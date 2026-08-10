namespace MotoSOS.API.Modules.NotificationOutbox.Worker;

public sealed class NotificationOutboxWorkerOptions
{
    public bool Enabled { get; set; }
    public int IntervalSeconds { get; set; } = 60;
    public int MaxItemsPerRun { get; set; } = 20;
    public bool SimulateFailures { get; set; }
    public bool RunOnStartup { get; set; }
}
