namespace MotoSOS.API.Modules.Onboarding.Contracts;

public sealed record OnboardingSummaryDto(
    bool CanConfirm,
    bool IsConfirmed,
    bool IsOperational,
    int CompletedSteps,
    int ProgressPercentage,
    string CurrentStep,
    OnboardingSummaryUserDto User,
    OnboardingSummaryProfileDto? Profile,
    OnboardingSummaryVehicleDto? Vehicle,
    OnboardingSummaryEmergencyContactDto? EmergencyContact,
    OnboardingSummaryDeviceDto? MobileDevice,
    OnboardingSummaryDeviceDto? Smartwatch,
    OnboardingSummarySubscriptionDto? Subscription,
    IReadOnlyList<OnboardingStepResponse> Steps);

public sealed record OnboardingSummaryUserDto(string Id, string FullName, string Email, string? PhoneNumber, string Role);

public sealed record OnboardingSummaryProfileDto(string FullName, string? PhoneNumber, string? PrimaryCity, string? AddressOrZone);

public sealed record OnboardingSummaryVehicleDto(string Id, string? VehicleType, string? Brand, string? Model, int? Year, string? Alias);

public sealed record OnboardingSummaryEmergencyContactDto(string Id, string? FullName, string? PhoneNumber, string? Relationship, string InvitationStatus);

public sealed record OnboardingSummaryDeviceDto(string Id, string DeviceType, string DeviceName, string Platform, string LinkStatus, string ConnectionStatus, int? BatteryLevel);

public sealed record OnboardingSummarySubscriptionDto(string Id, string PlanTier, string Status, string Source);
