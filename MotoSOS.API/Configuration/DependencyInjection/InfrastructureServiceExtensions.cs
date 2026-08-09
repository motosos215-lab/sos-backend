using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Configuration.Options;
using MotoSOS.API.Infrastructure.DateTime;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Indexes;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Repositories;
using MotoSOS.API.Infrastructure.Persistence.MongoDb.Settings;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyResolution.Application;
using MotoSOS.API.Modules.Escalations.Application;
using MotoSOS.API.Modules.EvidenceAttachments.Application;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.LocationSharing.Application;
using MotoSOS.API.Modules.MinorEvents.Application;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.OfflineIngestion.Application;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.OperationalDashboard.Application;
using MotoSOS.API.Modules.Plans.Application;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.ReportExports.Application;
using MotoSOS.API.Modules.TelemetrySummary.Application;
using MotoSOS.API.Modules.Trips.Application;
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
            services.AddScoped<IDeviceActivationCodeRepository, MongoDeviceActivationCodeRepository>();
            services.AddScoped<IUserDeviceRepository, MongoUserDeviceRepository>();
            services.AddScoped<IUserSubscriptionRepository, MongoUserSubscriptionRepository>();
            services.AddScoped<IOnboardingConfirmationRepository, MongoOnboardingConfirmationRepository>();
            services.AddScoped<ITripRepository, MongoTripRepository>();
            services.AddScoped<IOfflineIngestionRepository, MongoOfflineIngestionRepository>();
            services.AddScoped<IIncidentRepository, MongoIncidentRepository>();
            services.AddScoped<IAlertDispatchRepository, MongoAlertDispatchRepository>();
            services.AddScoped<IAuditLogRepository, MongoAuditLogRepository>();
            services.AddScoped<INotificationDeliveryAttemptRepository, MongoNotificationDeliveryAttemptRepository>();
            services.AddScoped<IMonitorLinkedContactRepository, MongoEmergencyContactRepository>();
            services.AddScoped<INotificationAttemptMonitorRepository, MongoNotificationDeliveryAttemptRepository>();
            services.AddScoped<IAlertAcknowledgementRepository, MongoAlertAcknowledgementRepository>();
            services.AddScoped<ILocationSharingRepository, MongoLocationSharingRepository>();
            services.AddScoped<IEmergencyResolutionRepository, MongoEmergencyResolutionRepository>();
            services.AddScoped<IEmergencyEscalationRepository, MongoEmergencyEscalationRepository>();
            services.AddScoped<IMinorEventRepository, MongoMinorEventRepository>();
            services.AddScoped<ITelemetrySummaryRepository, MongoTelemetrySummaryRepository>();
            services.AddScoped<IEvidenceAttachmentRepository, MongoEvidenceAttachmentRepository>();
            services.AddScoped<IResolutionReportExportRepository, MongoResolutionReportExportRepository>();
            services.AddScoped<IOperationalDashboardRepository, MongoOperationalDashboardRepository>();
        }
        else
        {
            services.AddScoped<IUserRepository, UnconfiguredUserRepository>();
            services.AddScoped<IRefreshTokenRepository, UnconfiguredRefreshTokenRepository>();
            services.AddScoped<IDriverProfileRepository, UnconfiguredDriverProfileRepository>();
            services.AddScoped<IDriverVehicleRepository, UnconfiguredDriverVehicleRepository>();
            services.AddScoped<IEmergencyContactRepository, UnconfiguredEmergencyContactRepository>();
            services.AddScoped<IDeviceActivationCodeRepository, UnconfiguredDeviceActivationCodeRepository>();
            services.AddScoped<IUserDeviceRepository, UnconfiguredUserDeviceRepository>();
            services.AddScoped<IUserSubscriptionRepository, UnconfiguredUserSubscriptionRepository>();
            services.AddScoped<IOnboardingConfirmationRepository, UnconfiguredOnboardingConfirmationRepository>();
            services.AddScoped<ITripRepository, UnconfiguredTripRepository>();
            services.AddScoped<IOfflineIngestionRepository, UnconfiguredOfflineIngestionRepository>();
            services.AddScoped<IIncidentRepository, UnconfiguredIncidentRepository>();
            services.AddScoped<IAlertDispatchRepository, UnconfiguredAlertDispatchRepository>();
            services.AddScoped<IAuditLogRepository, UnconfiguredAuditLogRepository>();
            services.AddScoped<INotificationDeliveryAttemptRepository, UnconfiguredNotificationDeliveryAttemptRepository>();
            services.AddScoped<IMonitorLinkedContactRepository, UnconfiguredMonitorLinkedContactRepository>();
            services.AddScoped<INotificationAttemptMonitorRepository, UnconfiguredNotificationAttemptMonitorRepository>();
            services.AddScoped<IAlertAcknowledgementRepository, UnconfiguredAlertAcknowledgementRepository>();
            services.AddScoped<ILocationSharingRepository, UnconfiguredLocationSharingRepository>();
            services.AddScoped<IEmergencyResolutionRepository, UnconfiguredEmergencyResolutionRepository>();
            services.AddScoped<IEmergencyEscalationRepository, UnconfiguredEmergencyEscalationRepository>();
            services.AddScoped<IMinorEventRepository, UnconfiguredMinorEventRepository>();
            services.AddScoped<ITelemetrySummaryRepository, UnconfiguredTelemetrySummaryRepository>();
            services.AddScoped<IEvidenceAttachmentRepository, UnconfiguredEvidenceAttachmentRepository>();
            services.AddScoped<IResolutionReportExportRepository, UnconfiguredResolutionReportExportRepository>();
            services.AddScoped<IOperationalDashboardRepository, UnconfiguredOperationalDashboardRepository>();
        }

        return services;
    }
}
