using FluentValidation;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.Trips.Contracts;

namespace MotoSOS.API.Modules.Trips.Application;

public sealed class CreateTripRoutePointsBatchRequestValidator : AbstractValidator<CreateTripRoutePointsBatchRequest>
{
    private const int MaxFutureSkewMinutes = 5;

    public CreateTripRoutePointsBatchRequestValidator(IOptions<TripRoutePointOptions> options, IClock clock)
    {
        int maxBatchSize = Math.Clamp(options.Value.MaxBatchSize, 1, 500);
        DateTimeOffset maxRecordedAtUtc = clock.UtcNow.AddMinutes(MaxFutureSkewMinutes);

        RuleFor(request => request.Points).NotNull().WithMessage("points is required.");
        RuleFor(request => request.Points).Must(points => points is not null && points.Count > 0).WithMessage("points must not be empty.");
        RuleFor(request => request.Points).Must(points => points is null || points.Count <= maxBatchSize).WithMessage($"points cannot contain more than {maxBatchSize} items.");
        RuleFor(request => request.Points).Must(NoDuplicateClientRoutePointIds).WithMessage("clientRoutePointId values must be unique within the batch.");

        RuleForEach(request => request.Points).ChildRules(point =>
        {
            point.RuleFor(p => p.ClientRoutePointId).NotEmpty().Must(IsGuid).WithMessage("clientRoutePointId must be a valid UUID.");
            point.RuleFor(p => p.Sequence).NotNull().GreaterThanOrEqualTo(1);
            point.RuleFor(p => p.RecordedAtUtc).NotNull().LessThanOrEqualTo(maxRecordedAtUtc).WithMessage("recordedAtUtc cannot be too far in the future.");
            point.RuleFor(p => p.Latitude).NotNull().InclusiveBetween(-90, 90);
            point.RuleFor(p => p.Longitude).NotNull().InclusiveBetween(-180, 180);
            point.RuleFor(p => p.AccuracyMeters).NotNull().InclusiveBetween(0, 5000);
            point.RuleFor(p => p.SpeedMetersPerSecond).InclusiveBetween(0, 120).When(p => p.SpeedMetersPerSecond.HasValue);
            point.RuleFor(p => p.BearingDegrees).InclusiveBetween(0, 360).When(p => p.BearingDegrees.HasValue);
            point.RuleFor(p => p.AppVersion).MaximumLength(50);
            point.RuleFor(p => p.DeviceId).MaximumLength(100);
        });
    }

    private static bool NoDuplicateClientRoutePointIds(IReadOnlyList<CreateTripRoutePointRequest>? points) => points is null || points
        .Where(point => !string.IsNullOrWhiteSpace(point.ClientRoutePointId))
        .Select(point => point.ClientRoutePointId!.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count() == points.Count(point => !string.IsNullOrWhiteSpace(point.ClientRoutePointId));

    private static bool IsGuid(string? value) => Guid.TryParse(value, out _);
}
