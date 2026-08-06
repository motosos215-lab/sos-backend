using FluentValidation;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Plans.Application;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Vehicles.Application;

namespace MotoSOS.API.Configuration.DependencyInjection;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<AuthService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<IOnboardingSummaryService, OnboardingSummaryService>();
        services.AddScoped<IOnboardingConfirmationService, OnboardingConfirmationService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IEmergencyContactService, EmergencyContactService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IPlanCatalogService, PlanCatalogService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<ITripService, TripService>();
        services.AddSingleton<ILinkingCodeGenerator, LinkingCodeGenerator>();
        services.AddSingleton<IActivationCodeGenerator, ActivationCodeGenerator>();
        services.AddSingleton<IDeviceIdentifierHasher, DeviceIdentifierHasher>();

        return services;
    }
}
