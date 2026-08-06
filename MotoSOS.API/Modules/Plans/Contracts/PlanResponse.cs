namespace MotoSOS.API.Modules.Plans.Contracts;

public sealed record PlanResponse(
    string Tier,
    string Name,
    string Description,
    bool IsDefault,
    bool IsSelectableInWeb,
    bool IsPaid,
    IReadOnlyList<string> Benefits,
    PlanLimitsResponse Limits);
