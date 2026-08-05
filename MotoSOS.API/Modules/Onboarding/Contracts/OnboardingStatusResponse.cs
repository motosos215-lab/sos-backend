namespace MotoSOS.API.Modules.Onboarding.Contracts;

public sealed record OnboardingStatusResponse(
    int TotalSteps,
    int CompletedSteps,
    int ProgressPercentage,
    string CurrentStep,
    bool IsOperational,
    IReadOnlyList<OnboardingStepResponse> Steps);
