using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
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
    private readonly IEmergencyContactRepository _emergencyContacts;
    private readonly IUserDeviceRepository _devices;

    public OnboardingService(IUserRepository users, IDriverProfileRepository profiles, IDriverVehicleRepository vehicles, IEmergencyContactRepository emergencyContacts, IUserDeviceRepository devices)
    {
        _users = users;
        _profiles = profiles;
        _vehicles = vehicles;
        _emergencyContacts = emergencyContacts;
        _devices = devices;
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
        IReadOnlyList<EmergencyContact> emergencyContacts = await _emergencyContacts.GetActiveByUserIdAsync(user.Id, cancellationToken);
        OnboardingStepStatus emergencyContactsStatus = GetEmergencyContactsStatus(vehicleStatus, emergencyContacts);
        bool hasMobileApp = await _devices.HasActiveLinkedMobileAppAsync(user.Id, cancellationToken);
        OnboardingStepStatus devicesStatus = GetDevicesStatus(emergencyContactsStatus, hasMobileApp);
        int completedSteps = GetCompletedSteps(profileStatus, vehicleStatus, emergencyContactsStatus, devicesStatus);
        string currentStep = GetCurrentStep(profileStatus, vehicleStatus, emergencyContactsStatus, devicesStatus);

        IReadOnlyList<OnboardingStepResponse> steps =
        [
            Step(OnboardingStepKey.Account, 1, "Cuenta", OnboardingStepStatus.Completed),
            Step(OnboardingStepKey.Profile, 2, "Perfil", profileStatus),
            Step(OnboardingStepKey.Vehicle, 3, "Motocicleta / Motoneta", vehicleStatus),
            Step(OnboardingStepKey.EmergencyContacts, 4, "Contactos de emergencia", emergencyContactsStatus),
            Step(OnboardingStepKey.Devices, 5, "Vinculación de dispositivos", devicesStatus),
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

    private static OnboardingStepStatus GetEmergencyContactsStatus(OnboardingStepStatus vehicleStatus, IReadOnlyList<EmergencyContact> emergencyContacts)
    {
        if (vehicleStatus != OnboardingStepStatus.Completed)
        {
            return OnboardingStepStatus.Pending;
        }

        if (emergencyContacts.Any(contact => contact.InvitationStatus is EmergencyContactInvitationStatus.Invited or EmergencyContactInvitationStatus.Linked))
        {
            return OnboardingStepStatus.Completed;
        }

        return emergencyContacts.Count > 0 ? OnboardingStepStatus.InProgress : OnboardingStepStatus.Pending;
    }

    private static OnboardingStepStatus GetDevicesStatus(OnboardingStepStatus emergencyContactsStatus, bool hasMobileApp)
    {
        if (emergencyContactsStatus != OnboardingStepStatus.Completed)
        {
            return OnboardingStepStatus.Pending;
        }

        return hasMobileApp ? OnboardingStepStatus.Completed : OnboardingStepStatus.Pending;
    }

    private static int GetCompletedSteps(OnboardingStepStatus profileStatus, OnboardingStepStatus vehicleStatus, OnboardingStepStatus emergencyContactsStatus, OnboardingStepStatus devicesStatus)
    {
        if (profileStatus != OnboardingStepStatus.Completed)
        {
            return 1;
        }

        if (vehicleStatus != OnboardingStepStatus.Completed)
        {
            return 2;
        }

        if (emergencyContactsStatus != OnboardingStepStatus.Completed)
        {
            return 3;
        }

        return devicesStatus == OnboardingStepStatus.Completed ? 5 : 4;
    }

    private static string GetCurrentStep(OnboardingStepStatus profileStatus, OnboardingStepStatus vehicleStatus, OnboardingStepStatus emergencyContactsStatus, OnboardingStepStatus devicesStatus)
    {
        if (profileStatus != OnboardingStepStatus.Completed)
        {
            return OnboardingStepKey.Profile.ToString();
        }

        if (vehicleStatus != OnboardingStepStatus.Completed)
        {
            return OnboardingStepKey.Vehicle.ToString();
        }

        if (emergencyContactsStatus != OnboardingStepStatus.Completed)
        {
            return OnboardingStepKey.EmergencyContacts.ToString();
        }

        return devicesStatus == OnboardingStepStatus.Completed
            ? OnboardingStepKey.Plan.ToString()
            : OnboardingStepKey.Devices.ToString();
    }

    private static int CalculateProgressPercentage(int completedSteps) =>
        (int)Math.Round(completedSteps * 100m / TotalSteps, MidpointRounding.AwayFromZero);

    private static OnboardingStepResponse Step(OnboardingStepKey key, int order, string label, OnboardingStepStatus status) =>
        new(key.ToString(), order, label, status.ToString());
}
