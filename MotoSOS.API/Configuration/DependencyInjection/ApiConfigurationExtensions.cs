namespace MotoSOS.API.Configuration.DependencyInjection;

public static class ApiConfigurationExtensions
{
    public const string CorsPolicyName = "MotoSosCorsPolicy";

    public static IServiceCollection AddApiConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddOpenApi();
        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyName, policy =>
            {
                string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

                policy
                    .WithOrigins(allowedOrigins)
                    .WithHeaders("Authorization", "Content-Type")
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS");
            });
        });

        return services;
    }
}
