using FluentValidation;
using MotoSOS.API.Modules.Devices.Contracts;
using MotoSOS.API.Modules.Devices.Domain;

namespace MotoSOS.API.Modules.Devices.Application;

public sealed class LinkSmartwatchRequestValidator : AbstractValidator<LinkSmartwatchRequest>
{
    public LinkSmartwatchRequestValidator()
    {
        RuleFor(request => request.ParentDeviceId).NotEmpty();
        RuleFor(request => request.DeviceName).NotEmpty().MaximumLength(120);
        RuleFor(request => request.Platform).NotEmpty().Must(BeAllowedPlatform).WithMessage("Platform is not allowed.");
        RuleFor(request => request.BatteryLevel).InclusiveBetween(0, 100).When(request => request.BatteryLevel.HasValue);
        RuleFor(request => request.Manufacturer).MaximumLength(80).When(request => !string.IsNullOrWhiteSpace(request.Manufacturer));
        RuleFor(request => request.Model).MaximumLength(80).When(request => !string.IsNullOrWhiteSpace(request.Model));
        RuleFor(request => request.OperatingSystemVersion).MaximumLength(50).When(request => !string.IsNullOrWhiteSpace(request.OperatingSystemVersion));
        RuleFor(request => request.AppVersion).MaximumLength(50).When(request => !string.IsNullOrWhiteSpace(request.AppVersion));
        RuleFor(request => request.DeviceIdentifier).MaximumLength(200).When(request => !string.IsNullOrWhiteSpace(request.DeviceIdentifier));
    }

    private static bool BeAllowedPlatform(string? value) => Enum.TryParse(value, ignoreCase: true, out DevicePlatform _);
}
