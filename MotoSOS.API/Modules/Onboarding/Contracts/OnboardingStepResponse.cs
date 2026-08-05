namespace MotoSOS.API.Modules.Onboarding.Contracts;

public sealed record OnboardingStepResponse(
    string Key,
    int Order,
    string Label,
    string Status);
