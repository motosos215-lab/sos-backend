using FluentValidation;
using MotoSOS.API.Modules.AlertAcknowledgements.Application;
using MotoSOS.API.Modules.AlertDispatch.Application;
using MotoSOS.API.Modules.AuditLogs.Application;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyResolution.Application;
using MotoSOS.API.Modules.EmergencyStatus.Application;
using MotoSOS.API.Modules.Escalations.Application;
using MotoSOS.API.Modules.Escalations.Worker;
using MotoSOS.API.Modules.Incidents.Application;
using MotoSOS.API.Modules.LocationSharing.Application;
using MotoSOS.API.Modules.MinorEvents.Application;
using MotoSOS.API.Modules.NotificationOutbox.Application;
using MotoSOS.API.Modules.NotificationOutbox.Worker;
using MotoSOS.API.Modules.Notifications.Application;
using MotoSOS.API.Modules.Notifications.Providers;
using MotoSOS.API.Modules.OfflineIngestion.Application;
using MotoSOS.API.Modules.OfflineProcessing.Application;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.OperationalDashboard.Application;
using MotoSOS.API.Modules.Plans.Application;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.TelemetrySummary.Application;
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
        services.AddScoped<ITelemetrySummaryService, TelemetrySummaryService>();
        services.AddScoped<IOfflineIngestionService, OfflineIngestionService>();
        services.AddScoped<IOfflineProcessingService, OfflineProcessingService>();
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<IAlertDispatchService, AlertDispatchService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationOutboxService, NotificationOutboxService>();
        services.AddOptions<NotificationOutboxWorkerOptions>();
        services.AddSingleton<INotificationOutboxWorkerStateStore, InMemoryNotificationOutboxWorkerStateStore>();
        services.AddHostedService<NotificationOutboxWorker>();
        services.AddScoped<SimulatedNotificationProvider>();
        services.AddScoped<INotificationProviderResolver, NotificationProviderResolver>();
        services.AddScoped<IAlertAcknowledgementService, AlertAcknowledgementService>();
        services.AddScoped<ILocationSharingService, LocationSharingService>();
        services.AddScoped<IEmergencyStatusService, EmergencyStatusService>();
        services.AddScoped<IEmergencyResolutionService, EmergencyResolutionService>();
        services.AddScoped<IOperationalDashboardService, OperationalDashboardService>();
        services.AddScoped<IEmergencyEscalationService, EmergencyEscalationService>();
        services.AddScoped<IAutomaticEscalationService, AutomaticEscalationService>();
        services.AddOptions<AutomaticEscalationWorkerOptions>();
        services.AddSingleton<IAutomaticEscalationWorkerStateStore, InMemoryAutomaticEscalationWorkerStateStore>();
        services.AddHostedService<AutomaticEscalationWorker>();
        services.AddScoped<IMinorEventService, MinorEventService>();
        services.AddSingleton<AuditLogQueryValidator>();
        services.AddSingleton<EscalationQueryValidator>();
        services.AddSingleton<MinorEventQueryValidator>();
        services.AddSingleton<TelemetrySummaryQueryValidator>();
        services.AddSingleton<OperationalDashboardQueryValidator>();
        services.AddSingleton<ILinkingCodeGenerator, LinkingCodeGenerator>();
        services.AddSingleton<IActivationCodeGenerator, ActivationCodeGenerator>();
        services.AddSingleton<IDeviceIdentifierHasher, DeviceIdentifierHasher>();
        services.AddSingleton<IOfflineIngestionIdempotencyKeyFactory, OfflineIngestionIdempotencyKeyFactory>();
        services.AddSingleton<IPayloadHasher, PayloadHasher>();
        services.AddSingleton<IIncidentIdempotencyKeyFactory, IncidentIdempotencyKeyFactory>();
        services.AddSingleton<IAlertDispatchIdempotencyKeyFactory, AlertDispatchIdempotencyKeyFactory>();
        services.AddSingleton<INotificationIdempotencyKeyFactory, NotificationIdempotencyKeyFactory>();
        services.AddSingleton<IAlertAcknowledgementIdempotencyKeyFactory, AlertAcknowledgementIdempotencyKeyFactory>();
        services.AddSingleton<ILocationSharingStalenessService, LocationSharingStalenessService>();
        services.AddSingleton<IEmergencyResolutionIdempotencyKeyFactory, EmergencyResolutionIdempotencyKeyFactory>();
        services.AddSingleton<IEmergencyEscalationIdempotencyKeyFactory, EmergencyEscalationIdempotencyKeyFactory>();
        services.AddSingleton<IMinorEventIdempotencyKeyFactory, MinorEventIdempotencyKeyFactory>();

        return services;
    }
}
