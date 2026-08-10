using MotoSOS.API.Modules.Escalations.Contracts;

namespace MotoSOS.API.Modules.Escalations.Application;

public interface IAutomaticEscalationService
{
    Task<RunAutomaticEscalationResponse> RunForAdminAsync(string adminUserId, RunAutomaticEscalationRequest request, CancellationToken cancellationToken);
    Task<RunAutomaticEscalationResponse> RunAsync(RunAutomaticEscalationRequest request, string runSource, CancellationToken cancellationToken);
    Task<AutomaticEscalationWorkerStatusResponse> GetWorkerStatusAsync(string adminUserId, CancellationToken cancellationToken);
}
