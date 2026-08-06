using MotoSOS.API.Modules.Plans.Contracts;
using MotoSOS.API.Modules.Plans.Domain;

namespace MotoSOS.API.Modules.Plans.Application;

public sealed class PlanCatalogService : IPlanCatalogService
{
    private static readonly IReadOnlyList<PlanDefinition> Plans =
    [
        new(
            PlanTier.Basic,
            "Básico",
            "Plan incluido con tu cuenta.",
            IsDefault: true,
            IsSelectableInWeb: true,
            IsPaid: false,
            ["1 contacto de emergencia", "1 vehículo", "App móvil", "Smartwatch", "Viaje monitoreado", "Detección de accidente", "Botón SOS", "Ubicación en emergencia", "Historial básico"],
            new PlanLimits(1, 1),
            AllowsSmartwatch: true,
            AllowsMonitoredTrip: true,
            AllowsAccidentDetection: true,
            AllowsSosButton: true,
            AllowsEmergencyLocation: true,
            AllowsBasicHistory: true,
            AllowsExtendedHistory: false,
            AllowsAutomaticEscalation: false,
            AllowsReports: false,
            AllowsMultipleDrivers: false,
            AllowsFamilyPanel: false),
        new(
            PlanTier.Plus,
            "Plus",
            "Más protección y control para tu día a día.",
            IsDefault: false,
            IsSelectableInWeb: false,
            IsPaid: true,
            ["Hasta 5 contactos de emergencia", "Más de un vehículo", "Escalamiento automático", "Más canales de notificación", "Historial extendido", "Reportes básicos"],
            new PlanLimits(5, 3),
            AllowsSmartwatch: true,
            AllowsMonitoredTrip: true,
            AllowsAccidentDetection: true,
            AllowsSosButton: true,
            AllowsEmergencyLocation: true,
            AllowsBasicHistory: true,
            AllowsExtendedHistory: true,
            AllowsAutomaticEscalation: true,
            AllowsReports: true,
            AllowsMultipleDrivers: false,
            AllowsFamilyPanel: false),
        new(
            PlanTier.FamilyPro,
            "Familiar / Pro",
            "Para familias y grupos que viajan juntos.",
            IsDefault: false,
            IsSelectableInWeb: false,
            IsPaid: true,
            ["Múltiples conductores", "Varios vehículos", "Panel familiar", "Reportes avanzados"],
            new PlanLimits(10, 10),
            AllowsSmartwatch: true,
            AllowsMonitoredTrip: true,
            AllowsAccidentDetection: true,
            AllowsSosButton: true,
            AllowsEmergencyLocation: true,
            AllowsBasicHistory: true,
            AllowsExtendedHistory: true,
            AllowsAutomaticEscalation: true,
            AllowsReports: true,
            AllowsMultipleDrivers: true,
            AllowsFamilyPanel: true)
    ];

    public GetPlansResponse GetPlans() => new(Plans.Select(ToResponse).ToArray());

    public PlanDefinition GetDefaultPlan() => Plans.Single(plan => plan.IsDefault);

    public PlanResponse ToResponse(PlanDefinition plan) => new(
        plan.Tier.ToString(),
        plan.Name,
        plan.Description,
        plan.IsDefault,
        plan.IsSelectableInWeb,
        plan.IsPaid,
        plan.Benefits,
        new PlanLimitsResponse(plan.Limits.MaxEmergencyContacts, plan.Limits.MaxVehicles));
}
