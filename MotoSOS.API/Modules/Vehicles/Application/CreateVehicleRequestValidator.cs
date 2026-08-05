using FluentValidation;
using MotoSOS.API.Modules.Vehicles.Contracts;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace MotoSOS.API.Modules.Vehicles.Application;

public sealed class CreateVehicleRequestValidator : AbstractValidator<CreateVehicleRequest>
{
    public CreateVehicleRequestValidator()
    {
        VehicleValidationRules.Apply(this);
    }
}

internal static class VehicleValidationRules
{
    public static void Apply<TRequest>(AbstractValidator<TRequest> validator)
        where TRequest : class
    {
        validator.RuleFor(request => GetSaveMode(request))
            .NotEmpty()
            .Must(IsAllowedSaveMode)
            .WithName(nameof(CreateVehicleRequest.SaveMode))
            .WithMessage("Save mode must be Draft or Continue.");

        validator.RuleFor(request => GetVehicleType(request))
            .Must(BeAllowedVehicleType)
            .When(request => !string.IsNullOrWhiteSpace(GetVehicleType(request)))
            .WithName(nameof(CreateVehicleRequest.VehicleType))
            .WithMessage("Vehicle type is not allowed.");

        validator.RuleFor(request => GetPrimaryUse(request))
            .Must(BeAllowedPrimaryUse)
            .When(request => !string.IsNullOrWhiteSpace(GetPrimaryUse(request)))
            .WithName(nameof(CreateVehicleRequest.PrimaryUse))
            .WithMessage("Primary use is not allowed.");

        validator.RuleFor(request => GetUsageFrequency(request))
            .Must(BeAllowedUsageFrequency)
            .When(request => !string.IsNullOrWhiteSpace(GetUsageFrequency(request)))
            .WithName(nameof(CreateVehicleRequest.UsageFrequency))
            .WithMessage("Usage frequency is not allowed.");

        validator.RuleFor(request => GetYear(request))
            .InclusiveBetween(1950, DateTime.UtcNow.Year + 1)
            .When(request => GetYear(request).HasValue)
            .WithName(nameof(CreateVehicleRequest.Year));

        validator.RuleFor(request => GetBrand(request)).MaximumLength(80).When(request => !string.IsNullOrWhiteSpace(GetBrand(request))).WithName(nameof(CreateVehicleRequest.Brand));
        validator.RuleFor(request => GetModel(request)).MaximumLength(80).When(request => !string.IsNullOrWhiteSpace(GetModel(request))).WithName(nameof(CreateVehicleRequest.Model));
        validator.RuleFor(request => GetAlias(request)).MaximumLength(80).When(request => !string.IsNullOrWhiteSpace(GetAlias(request))).WithName(nameof(CreateVehicleRequest.Alias));
        validator.RuleFor(request => GetColor(request)).MaximumLength(50).When(request => !string.IsNullOrWhiteSpace(GetColor(request))).WithName(nameof(CreateVehicleRequest.Color));
        validator.RuleFor(request => GetPlateNumber(request)).MaximumLength(20).When(request => !string.IsNullOrWhiteSpace(GetPlateNumber(request))).WithName(nameof(CreateVehicleRequest.PlateNumber));
        validator.RuleFor(request => GetVin(request)).MaximumLength(40).When(request => !string.IsNullOrWhiteSpace(GetVin(request))).WithName(nameof(CreateVehicleRequest.Vin));

        validator.When(IsContinue, () =>
        {
            validator.RuleFor(request => GetVehicleType(request)).NotEmpty().Must(BeAllowedVehicleType).WithName(nameof(CreateVehicleRequest.VehicleType));
            validator.RuleFor(request => GetBrand(request)).NotEmpty().WithName(nameof(CreateVehicleRequest.Brand));
            validator.RuleFor(request => GetModel(request)).NotEmpty().WithName(nameof(CreateVehicleRequest.Model));
            validator.RuleFor(request => GetYear(request)).NotNull().InclusiveBetween(1950, DateTime.UtcNow.Year + 1).WithName(nameof(CreateVehicleRequest.Year));
            validator.RuleFor(request => GetAlias(request)).NotEmpty().WithName(nameof(CreateVehicleRequest.Alias));
            validator.RuleFor(request => GetPrimaryUse(request)).NotEmpty().Must(BeAllowedPrimaryUse).WithName(nameof(CreateVehicleRequest.PrimaryUse));
            validator.RuleFor(request => GetColor(request)).NotEmpty().WithName(nameof(CreateVehicleRequest.Color));
            validator.RuleFor(request => GetPlateNumber(request)).NotEmpty().WithName(nameof(CreateVehicleRequest.PlateNumber));
            validator.RuleFor(request => GetVin(request)).NotEmpty().WithName(nameof(CreateVehicleRequest.Vin));
            validator.RuleFor(request => GetUsageFrequency(request)).NotEmpty().Must(BeAllowedUsageFrequency).WithName(nameof(CreateVehicleRequest.UsageFrequency));
        });
    }

    private static bool IsContinue<TRequest>(TRequest request) where TRequest : class =>
        string.Equals(GetSaveMode(request)?.Trim(), nameof(SaveMode.Continue), StringComparison.OrdinalIgnoreCase);

    private static bool IsAllowedSaveMode(string? saveMode) =>
        string.Equals(saveMode?.Trim(), nameof(SaveMode.Draft), StringComparison.OrdinalIgnoreCase) ||
        string.Equals(saveMode?.Trim(), nameof(SaveMode.Continue), StringComparison.OrdinalIgnoreCase);

    private static bool BeAllowedVehicleType(string? value) => Enum.TryParse(value, ignoreCase: true, out VehicleType _);

    private static bool BeAllowedPrimaryUse(string? value) => Enum.TryParse(value, ignoreCase: true, out VehiclePrimaryUse _);

    private static bool BeAllowedUsageFrequency(string? value) => Enum.TryParse(value, ignoreCase: true, out VehicleUsageFrequency _);

    private static string? GetVehicleType<TRequest>(TRequest request) where TRequest : class => request switch
    {
        CreateVehicleRequest create => create.VehicleType,
        UpdateVehicleRequest update => update.VehicleType,
        _ => null
    };

    private static string? GetBrand<TRequest>(TRequest request) where TRequest : class => request switch { CreateVehicleRequest create => create.Brand, UpdateVehicleRequest update => update.Brand, _ => null };
    private static string? GetModel<TRequest>(TRequest request) where TRequest : class => request switch { CreateVehicleRequest create => create.Model, UpdateVehicleRequest update => update.Model, _ => null };
    private static int? GetYear<TRequest>(TRequest request) where TRequest : class => request switch { CreateVehicleRequest create => create.Year, UpdateVehicleRequest update => update.Year, _ => null };
    private static string? GetAlias<TRequest>(TRequest request) where TRequest : class => request switch { CreateVehicleRequest create => create.Alias, UpdateVehicleRequest update => update.Alias, _ => null };
    private static string? GetPrimaryUse<TRequest>(TRequest request) where TRequest : class => request switch { CreateVehicleRequest create => create.PrimaryUse, UpdateVehicleRequest update => update.PrimaryUse, _ => null };
    private static string? GetColor<TRequest>(TRequest request) where TRequest : class => request switch { CreateVehicleRequest create => create.Color, UpdateVehicleRequest update => update.Color, _ => null };
    private static string? GetPlateNumber<TRequest>(TRequest request) where TRequest : class => request switch { CreateVehicleRequest create => create.PlateNumber, UpdateVehicleRequest update => update.PlateNumber, _ => null };
    private static string? GetVin<TRequest>(TRequest request) where TRequest : class => request switch { CreateVehicleRequest create => create.Vin, UpdateVehicleRequest update => update.Vin, _ => null };
    private static string? GetUsageFrequency<TRequest>(TRequest request) where TRequest : class => request switch { CreateVehicleRequest create => create.UsageFrequency, UpdateVehicleRequest update => update.UsageFrequency, _ => null };
    private static string? GetSaveMode<TRequest>(TRequest request) where TRequest : class => request switch { CreateVehicleRequest create => create.SaveMode, UpdateVehicleRequest update => update.SaveMode, _ => null };
}
