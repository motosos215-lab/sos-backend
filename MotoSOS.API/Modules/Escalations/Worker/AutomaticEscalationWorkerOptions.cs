namespace MotoSOS.API.Modules.Escalations.Worker;

public sealed class AutomaticEscalationWorkerOptions
{
    public bool Enabled { get; set; }
    public int IntervalSeconds { get; set; } = 60;
    public int MaxItemsPerRun { get; set; } = 20;
    public int EscalateAfterSeconds { get; set; } = 300;
    public bool RunOnStartup { get; set; }
}
