namespace MotoSOS.API.Modules.Plans.Contracts;

public sealed record GetPlansResponse(IReadOnlyList<PlanResponse> Plans);
