namespace MotoSOS.API.Configuration.DependencyInjection;

public static class SecurityServiceExtensions
{
    public static IServiceCollection AddSecurityServices(this IServiceCollection services)
    {
        services.AddAuthorization();

        return services;
    }
}
