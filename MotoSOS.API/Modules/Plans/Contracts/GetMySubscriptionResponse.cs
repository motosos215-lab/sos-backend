namespace MotoSOS.API.Modules.Plans.Contracts;

public sealed record GetMySubscriptionResponse(SubscriptionResponse? Subscription, PlanResponse? DefaultPlan);
