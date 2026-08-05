using FluentValidation;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Users.Application;

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

        return services;
    }
}
