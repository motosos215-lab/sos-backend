using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Configuration.Options;
using MotoSOS.API.Infrastructure.DateTime;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Settings;

namespace MotoSOS.API.Configuration.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbOptions>(configuration.GetSection(MongoDbOptions.SectionName));
        services.AddOptions<MongoDbSettings>()
            .Bind(configuration.GetSection(MongoDbSettings.SectionName))
            .Validate(settings =>
                string.IsNullOrWhiteSpace(settings.ConnectionString) == string.IsNullOrWhiteSpace(settings.DatabaseName),
                "MongoDB settings must define both ConnectionString and DatabaseName, or neither while MongoDB is not connected.");

        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
