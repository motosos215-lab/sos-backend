using MotoSOS.API.Modules.Plans.Contracts;
using MotoSOS.API.Modules.Plans.Domain;

namespace MotoSOS.API.Modules.Plans.Application;

public interface IPlanCatalogService
{
    GetPlansResponse GetPlans();
    PlanDefinition GetDefaultPlan();
    PlanResponse ToResponse(PlanDefinition plan);
}
