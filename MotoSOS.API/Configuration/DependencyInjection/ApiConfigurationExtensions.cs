namespace MotoSOS.API.Configuration.DependencyInjection;

public static class ApiConfigurationExtensions
{
    public static IServiceCollection AddApiConfiguration(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddOpenApi();

        return services;
    }
}
