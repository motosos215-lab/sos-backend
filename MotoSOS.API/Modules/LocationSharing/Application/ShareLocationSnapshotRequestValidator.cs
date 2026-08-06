using FluentValidation;
using MotoSOS.API.Modules.LocationSharing.Contracts;
using MotoSOS.API.Modules.LocationSharing.Domain;

namespace MotoSOS.API.Modules.LocationSharing.Application;

public sealed class ShareLocationSnapshotRequestValidator : AbstractValidator<ShareLocationSnapshotRequest>
{
    public ShareLocationSnapshotRequestValidator()
    {
        RuleFor(r => r.IncidentId).NotEmpty();
        RuleFor(r => r.ClientLocationUpdateId).NotEmpty().Must(v => Guid.TryParse(v, out _)).WithMessage("ClientLocationUpdateId must be a valid UUID.");
        RuleFor(r => r.Latitude).NotNull().InclusiveBetween(-90, 90);
        RuleFor(r => r.Longitude).NotNull().InclusiveBetween(-180, 180);
        RuleFor(r => r.AccuracyMeters).GreaterThanOrEqualTo(0).When(r => r.AccuracyMeters.HasValue);
        RuleFor(r => r.SpeedMetersPerSecond).GreaterThanOrEqualTo(0).When(r => r.SpeedMetersPerSecond.HasValue);
        RuleFor(r => r.HeadingDegrees).InclusiveBetween(0, 360).When(r => r.HeadingDegrees.HasValue);
        RuleFor(r => r.BatteryPercentage).InclusiveBetween(0, 100).When(r => r.BatteryPercentage.HasValue);
        RuleFor(r => r.RecordedAtUtc).NotNull();
        RuleFor(r => r.Source).NotEmpty().Must(v => Enum.TryParse<LocationSharingSource>(v, true, out _));
    }
}
