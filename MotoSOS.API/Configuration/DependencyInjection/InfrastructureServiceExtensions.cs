using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Configuration.Options;
using MotoSOS.API.Infrastructure.DateTime;

namespace MotoSOS.API.Configuration.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbOptions>(configuration.GetSection(MongoDbOptions.SectionName));
        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
