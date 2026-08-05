using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Onboarding.Domain;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace MotoSOS.API.Modules.Onboarding.Application;

public sealed class OnboardingService : IOnboardingService
{
    private const int TotalSteps = 7;

    private readonly IUserRepository _users;
    private readonly IDriverProfileRepository _profiles;
    private readonly IDriverVehicleRepository _vehicles;

    public OnboardingService(IUserRepository users, IDriverProfileRepository profiles, IDriverVehicleRepository vehicles)
    {
        _users = users;
        _profiles = profiles;
        _vehicles = vehicles;
    }

    public async Task<OnboardingStatusResponse> GetStatusAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAppException("Invalid authentication credentials.");
        }

        if (user.Role != UserRole.Rider)
        {
            throw new ForbiddenAppException("This onboarding flow is available only for riders.");
        }

        DriverProfile? profile = await _profiles.GetByUserIdAsync(user.Id, cancellationToken);
        OnboardingStepStatus profileStatus = GetProfileStatus(profile);
        IReadOnlyList<DriverVehicle> vehicles = await _vehicles.GetActiveByUserIdAsync(user.Id, cancellationToken);
        OnboardingStepStatus vehicleStatus = GetVehicleStatus(profileStatus, vehicles);
        int completedSteps = GetCompletedSteps(profileStatus, vehicleStatus);
        string currentStep = GetCurrentStep(profileStatus, vehicleStatus);

        IReadOnlyList<OnboardingStepResponse> steps =
        [
            Step(OnboardingStepKey.Account, 1, "Cuenta", OnboardingStepStatus.Completed),
            Step(OnboardingStepKey.Profile, 2, "Perfil", profileStatus),
            Step(OnboardingStepKey.Vehicle, 3, "Motocicleta / Motoneta", vehicleStatus),
            Step(OnboardingStepKey.EmergencyContacts, 4, "Contactos de emergencia", OnboardingStepStatus.Pending),
            Step(OnboardingStepKey.Devices, 5, "Vinculación de dispositivos", OnboardingStepStatus.Pending),
            Step(OnboardingStepKey.Plan, 6, "Plan y licencia", OnboardingStepStatus.Pending),
            Step(OnboardingStepKey.Confirmation, 7, "Confirmación", OnboardingStepStatus.Pending)
        ];

        return new OnboardingStatusResponse(
            TotalSteps,
            completedSteps,
            CalculateProgressPercentage(completedSteps),
            currentStep,
            IsOperational: false,
            steps);
    }

    private static OnboardingStepStatus GetProfileStatus(DriverProfile? profile)
    {
        if (profile is null)
        {
            return OnboardingStepStatus.Pending;
        }

        return profile.CompletionStatus == ProfileCompletionStatus.Completed
            ? OnboardingStepStatus.Completed
            : OnboardingStepStatus.InProgress;
    }

    private static OnboardingStepStatus GetVehicleStatus(OnboardingStepStatus profileStatus, IReadOnlyList<DriverVehicle> vehicles)
    {
        if (profileStatus != OnboardingStepStatus.Completed)
        {
            return OnboardingStepStatus.Pending;
        }

        if (vehicles.Any(vehicle => vehicle.CompletionStatus == VehicleCompletionStatus.Completed))
        {
            return OnboardingStepStatus.Completed;
        }

        return vehicles.Count > 0 ? OnboardingStepStatus.InProgress : OnboardingStepStatus.Pending;
    }

    private static int GetCompletedSteps(OnboardingStepStatus profileStatus, OnboardingStepStatus vehicleStatus)
    {
        if (profileStatus != OnboardingStepStatus.Completed)
        {
            return 1;
        }

        return vehicleStatus == OnboardingStepStatus.Completed ? 3 : 2;
    }

    private static string GetCurrentStep(OnboardingStepStatus profileStatus, OnboardingStepStatus vehicleStatus)
    {
        if (profileStatus != OnboardingStepStatus.Completed)
        {
            return OnboardingStepKey.Profile.ToString();
        }

        return vehicleStatus == OnboardingStepStatus.Completed
            ? OnboardingStepKey.EmergencyContacts.ToString()
            : OnboardingStepKey.Vehicle.ToString();
    }

    private static int CalculateProgressPercentage(int completedSteps) =>
        (int)Math.Round(completedSteps * 100m / TotalSteps, MidpointRounding.AwayFromZero);

    private static OnboardingStepResponse Step(OnboardingStepKey key, int order, string label, OnboardingStepStatus status) =>
        new(key.ToString(), order, label, status.ToString());
}
