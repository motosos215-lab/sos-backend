using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Domain;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Onboarding.Domain;
using MotoSOS.API.Modules.Plans.Application;
using MotoSOS.API.Modules.Plans.Domain;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Profiles.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace MotoSOS.API.Modules.Onboarding.Application;

public sealed class OnboardingSummaryService : IOnboardingSummaryService
{
    private readonly IUserRepository _users;
    private readonly IDriverProfileRepository _profiles;
    private readonly IDriverVehicleRepository _vehicles;
    private readonly IEmergencyContactRepository _emergencyContacts;
    private readonly IUserDeviceRepository _devices;
    private readonly IUserSubscriptionRepository _subscriptions;
    private readonly IOnboardingConfirmationRepository _confirmations;
    private readonly IOnboardingService _onboarding;

    public OnboardingSummaryService(
        IUserRepository users,
        IDriverProfileRepository profiles,
        IDriverVehicleRepository vehicles,
        IEmergencyContactRepository emergencyContacts,
        IUserDeviceRepository devices,
        IUserSubscriptionRepository subscriptions,
        IOnboardingConfirmationRepository confirmations,
        IOnboardingService onboarding)
    {
        _users = users;
        _profiles = profiles;
        _vehicles = vehicles;
        _emergencyContacts = emergencyContacts;
        _devices = devices;
        _subscriptions = subscriptions;
        _confirmations = confirmations;
        _onboarding = onboarding;
    }

    public async Task<OnboardingSummaryResponse> GetSummaryAsync(string userId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        OnboardingStatusResponse status = await _onboarding.GetStatusAsync(user.Id, cancellationToken);
        DriverProfile? profile = await _profiles.GetByUserIdAsync(user.Id, cancellationToken);
        IReadOnlyList<DriverVehicle> vehicles = await _vehicles.GetActiveByUserIdAsync(user.Id, cancellationToken);
        IReadOnlyList<EmergencyContact> contacts = await _emergencyContacts.GetActiveByUserIdAsync(user.Id, cancellationToken);
        IReadOnlyList<UserDevice> devices = await _devices.GetActiveByUserIdAsync(user.Id, cancellationToken);
        UserSubscription? subscription = await _subscriptions.GetByUserIdAsync(user.Id, cancellationToken);
        OnboardingConfirmation? confirmation = await _confirmations.GetByUserIdAsync(user.Id, cancellationToken);

        bool previousStepsCompleted = status.Steps
            .Where(step => step.Key != OnboardingStepKey.Confirmation.ToString())
            .All(step => step.Status == OnboardingStepStatus.Completed.ToString());
        bool isConfirmed = previousStepsCompleted && confirmation?.IsOperational == true;

        var summary = new OnboardingSummaryDto(
            CanConfirm: previousStepsCompleted,
            IsConfirmed: isConfirmed,
            IsOperational: status.IsOperational,
            status.CompletedSteps,
            status.ProgressPercentage,
            status.CurrentStep,
            new OnboardingSummaryUserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString()),
            profile is null ? null : new OnboardingSummaryProfileDto(user.FullName, user.PhoneNumber, profile.PrimaryCity, profile.AddressOrZone),
            ToVehicleSummary(vehicles),
            ToEmergencyContactSummary(contacts),
            ToDeviceSummary(devices.FirstOrDefault(device => device.DeviceType == DeviceType.MobileApp && device.LinkStatus == DeviceLinkStatus.Linked)),
            ToDeviceSummary(devices.FirstOrDefault(device => device.DeviceType == DeviceType.Smartwatch && device.LinkStatus == DeviceLinkStatus.Linked)),
            subscription is null ? null : new OnboardingSummarySubscriptionDto(subscription.Id, subscription.PlanTier.ToString(), subscription.Status.ToString(), subscription.Source.ToString()),
            status.Steps);

        return new OnboardingSummaryResponse(summary);
    }

    private async Task<User> GetRiderUserAsync(string userId, CancellationToken cancellationToken)
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

        return user;
    }

    private static OnboardingSummaryVehicleDto? ToVehicleSummary(IReadOnlyList<DriverVehicle> vehicles)
    {
        DriverVehicle? vehicle = vehicles.FirstOrDefault(vehicle => vehicle.IsPrimary) ?? (vehicles.Count > 0 ? vehicles[0] : null);
        return vehicle is null ? null : new OnboardingSummaryVehicleDto(vehicle.Id, vehicle.VehicleType?.ToString(), vehicle.Brand, vehicle.Model, vehicle.Year, vehicle.Alias);
    }

    private static OnboardingSummaryEmergencyContactDto? ToEmergencyContactSummary(IReadOnlyList<EmergencyContact> contacts)
    {
        EmergencyContact? contact = contacts.FirstOrDefault(contact => contact.IsPrimary) ?? (contacts.Count > 0 ? contacts[0] : null);
        return contact is null ? null : new OnboardingSummaryEmergencyContactDto(contact.Id, contact.FullName, contact.PhoneNumber, contact.Relationship, contact.InvitationStatus.ToString());
    }

    private static OnboardingSummaryDeviceDto? ToDeviceSummary(UserDevice? device) => device is null
        ? null
        : new OnboardingSummaryDeviceDto(device.Id, device.DeviceType.ToString(), device.DeviceName, device.Platform.ToString(), device.LinkStatus.ToString(), device.ConnectionStatus.ToString(), device.BatteryLevel);
}
