using FluentValidation;
using MotoSOS.API.Modules.Devices.Contracts;
using MotoSOS.API.Modules.Devices.Domain;

namespace MotoSOS.API.Modules.Devices.Application;

public sealed class HeartbeatDeviceRequestValidator : AbstractValidator<HeartbeatDeviceRequest>
{
    public HeartbeatDeviceRequestValidator()
    {
        RuleFor(request => request.BatteryLevel).InclusiveBetween(0, 100).When(request => request.BatteryLevel.HasValue);
        RuleFor(request => request.ConnectionStatus).Must(BeAllowedConnectionStatus).When(request => !string.IsNullOrWhiteSpace(request.ConnectionStatus)).WithMessage("Connection status is not allowed.");
        RuleFor(request => request.AppVersion).MaximumLength(50).When(request => !string.IsNullOrWhiteSpace(request.AppVersion));
    }

    private static bool BeAllowedConnectionStatus(string? value) => Enum.TryParse(value, ignoreCase: true, out DeviceConnectionStatus _);
}
