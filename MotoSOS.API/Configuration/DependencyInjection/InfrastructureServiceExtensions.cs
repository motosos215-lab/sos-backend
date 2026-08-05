using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Configuration.Options;
using MotoSOS.API.Infrastructure.DateTime;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Indexes;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Settings;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Vehicles.Application;

namespace MotoSOS.API.Configuration.DependencyInjection;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        bool isTesting = environment.IsEnvironment("Testing");
        bool allowUnconfiguredMongoDb = environment.IsDevelopment() || isTesting;

        OptionsBuilder<MongoDbOptions> mongoDbOptionsBuilder = services.AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            .ValidateDataAnnotations();

        OptionsBuilder<MongoDbSettings> mongoOptionsBuilder = services.AddOptions<MongoDbSettings>()
            .Bind(configuration.GetSection(MongoDbSettings.SectionName))
            .ValidateDataAnnotations();

        if (!allowUnconfiguredMongoDb)
        {
            mongoDbOptionsBuilder.ValidateOnStart();
            mongoOptionsBuilder.ValidateOnStart();
        }

        services.AddSingleton<IClock, SystemClock>();

        var mongoSettings = configuration.GetSection(MongoDbSettings.SectionName).Get<MongoDbSettings>() ?? new MongoDbSettings();

        if (!isTesting && !string.IsNullOrWhiteSpace(mongoSettings.ConnectionString))
        {
            if (string.IsNullOrWhiteSpace(mongoSettings.DatabaseName))
            {
                throw new InvalidOperationException("MongoDB DatabaseName is required when ConnectionString is configured.");
            }

            services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoSettings.ConnectionString));
            services.AddSingleton(serviceProvider =>
                serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(mongoSettings.DatabaseName));
            services.AddSingleton<MongoIndexInitializer>();
            services.AddHostedService<MongoIndexInitializerHostedService>();
            services.AddScoped<IUserRepository, MongoUserRepository>();
            services.AddScoped<IRefreshTokenRepository, MongoRefreshTokenRepository>();
            services.AddScoped<IDriverProfileRepository, MongoDriverProfileRepository>();
            services.AddScoped<IDriverVehicleRepository, MongoDriverVehicleRepository>();
            services.AddScoped<IEmergencyContactRepository, MongoEmergencyContactRepository>();
        }
        else
        {
            services.AddScoped<IUserRepository, UnconfiguredUserRepository>();
            services.AddScoped<IRefreshTokenRepository, UnconfiguredRefreshTokenRepository>();
            services.AddScoped<IDriverProfileRepository, UnconfiguredDriverProfileRepository>();
            services.AddScoped<IDriverVehicleRepository, UnconfiguredDriverVehicleRepository>();
            services.AddScoped<IEmergencyContactRepository, UnconfiguredEmergencyContactRepository>();
        }

        return services;
    }
}
