using FluentValidation;
using MotoSOS.API.Modules.Devices.Contracts;
using MotoSOS.API.Modules.Devices.Domain;

namespace MotoSOS.API.Modules.Devices.Application;

public sealed class LinkMobileDeviceRequestValidator : AbstractValidator<LinkMobileDeviceRequest>
{
    private const string ActivationCodePattern = "^MSOS-[A-Z2-9]{4}-[A-Z2-9]{4}$";

    public LinkMobileDeviceRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty().Matches(ActivationCodePattern).WithMessage("Activation code format is invalid.");
        RuleFor(request => request.DeviceName).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Platform).NotEmpty().Must(BeAllowedPlatform).WithMessage("Platform is not allowed.");
        RuleFor(request => request.Manufacturer).MaximumLength(80).When(request => !string.IsNullOrWhiteSpace(request.Manufacturer));
        RuleFor(request => request.Model).MaximumLength(80).When(request => !string.IsNullOrWhiteSpace(request.Model));
        RuleFor(request => request.OperatingSystemVersion).MaximumLength(50).When(request => !string.IsNullOrWhiteSpace(request.OperatingSystemVersion));
        RuleFor(request => request.AppVersion).MaximumLength(50).When(request => !string.IsNullOrWhiteSpace(request.AppVersion));
        RuleFor(request => request.DeviceIdentifier).MaximumLength(200).When(request => !string.IsNullOrWhiteSpace(request.DeviceIdentifier));
    }

    private static bool BeAllowedPlatform(string? value) => Enum.TryParse(value, ignoreCase: true, out DevicePlatform _);
}
