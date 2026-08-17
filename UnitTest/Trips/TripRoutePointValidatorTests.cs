using FluentAssertions;
using Microsoft.Extensions.Options;
using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Modules.Trips.Application;
using MotoSOS.API.Modules.Trips.Contracts;

namespace UnitTest.Trips;

public sealed class TripRoutePointValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RejectsEmptyBatchAndBatchOverMaxSize()
    {
        var validator = CreateValidator(maxBatchSize: 1);

        validator.Validate(new CreateTripRoutePointsBatchRequest([])).IsValid.Should().BeFalse();
        validator.Validate(new CreateTripRoutePointsBatchRequest([Point(), Point(Guid.NewGuid().ToString(), 2)])).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RejectsInvalidCoordinatesAccuracySpeedBearingFutureSequenceAndClientId()
    {
        var validator = CreateValidator();

        validator.Validate(new CreateTripRoutePointsBatchRequest([Point(latitude: -91)])).IsValid.Should().BeFalse();
        validator.Validate(new CreateTripRoutePointsBatchRequest([Point(longitude: -181)])).IsValid.Should().BeFalse();
        validator.Validate(new CreateTripRoutePointsBatchRequest([Point(accuracy: 5001)])).IsValid.Should().BeFalse();
        validator.Validate(new CreateTripRoutePointsBatchRequest([Point(speed: -1)])).IsValid.Should().BeFalse();
        validator.Validate(new CreateTripRoutePointsBatchRequest([Point(bearing: 361)])).IsValid.Should().BeFalse();
        validator.Validate(new CreateTripRoutePointsBatchRequest([Point(recordedAtUtc: Now.AddMinutes(6))])).IsValid.Should().BeFalse();
        validator.Validate(new CreateTripRoutePointsBatchRequest([Point(sequence: 0)])).IsValid.Should().BeFalse();
        validator.Validate(new CreateTripRoutePointsBatchRequest([Point(clientId: "not-a-guid")])).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RejectsDuplicateClientRoutePointIdsInsideBatch()
    {
        var validator = CreateValidator();
        string clientId = Guid.NewGuid().ToString();

        bool isValid = validator.Validate(new CreateTripRoutePointsBatchRequest([Point(clientId, 1), Point(clientId, 2)])).IsValid;

        isValid.Should().BeFalse();
    }

    private static CreateTripRoutePointsBatchRequestValidator CreateValidator(int maxBatchSize = 500) => new(Options.Create(new TripRoutePointOptions { MaxBatchSize = maxBatchSize }), new Clock());

    private static CreateTripRoutePointRequest Point(string? clientId = null, int sequence = 1, DateTimeOffset? recordedAtUtc = null, double latitude = 19.4326, double longitude = -99.1332, double accuracy = 12.5, double? speed = 8.4, double? bearing = 180) =>
        new(clientId ?? Guid.NewGuid().ToString(), sequence, recordedAtUtc ?? Now, latitude, longitude, accuracy, speed, bearing);

    private sealed class Clock : IClock { public DateTimeOffset UtcNow => Now; }
}
