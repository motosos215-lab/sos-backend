using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Configuration.Options;
using MotoSOS.API.Infrastructure.DateTime;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Settings;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Users.Application;
using MongoDB.Driver;

namespace MotoSOS.API.Configuration.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbOptions>(configuration.GetSection(MongoDbOptions.SectionName));
        services.AddOptions<MongoDbSettings>()
            .Bind(configuration.GetSection(MongoDbSettings.SectionName))
            .Validate(settings =>
                string.IsNullOrWhiteSpace(settings.ConnectionString) || !string.IsNullOrWhiteSpace(settings.DatabaseName),
                "MongoDB DatabaseName is required when ConnectionString is configured.");

        services.AddSingleton<IClock, SystemClock>();

        var mongoSettings = configuration.GetSection(MongoDbSettings.SectionName).Get<MongoDbSettings>() ?? new MongoDbSettings();

        if (!string.IsNullOrWhiteSpace(mongoSettings.ConnectionString))
        {
            services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings.ConnectionString));
            services.AddSingleton(serviceProvider =>
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(mongoSettings.DatabaseName));
            services.AddScoped<IUserRepository, MongoUserRepository>();
            services.AddScoped<IRefreshTokenRepository, MongoRefreshTokenRepository>();
        }
        else
        {
            services.AddScoped<IUserRepository, UnconfiguredUserRepository>();
            services.AddScoped<IRefreshTokenRepository, UnconfiguredRefreshTokenRepository>();
        }

        return services;
    }
}
