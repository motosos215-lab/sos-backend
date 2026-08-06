using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Devices.Application;
using MotoSOS.API.Modules.Devices.Domain;
using MotoSOS.API.Modules.Onboarding.Application;
using MotoSOS.API.Modules.Onboarding.Contracts;
using MotoSOS.API.Modules.Trips.Contracts;
using MotoSOS.API.Modules.Trips.Domain;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace MotoSOS.API.Modules.Trips.Application;

public sealed class TripService : ITripService
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IUserRepository _users;
    private readonly IOnboardingService _onboarding;
    private readonly IDriverVehicleRepository _vehicles;
    private readonly IUserDeviceRepository _devices;
    private readonly ITripRepository _trips;
    private readonly IClock _clock;

    public TripService(IUserRepository users, IOnboardingService onboarding, IDriverVehicleRepository vehicles, IUserDeviceRepository devices, ITripRepository trips, IClock clock)
    {
        _users = users;
        _onboarding = onboarding;
        _vehicles = vehicles;
        _devices = devices;
        _trips = trips;
        _clock = clock;
    }

    public async Task<GetActiveTripResponse> GetActiveAsync(string userId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        Trip? trip = await _trips.GetActiveByUserIdAsync(user.Id, cancellationToken);
        return new GetActiveTripResponse(trip is null ? null : ToResponse(trip));
    }

    public async Task<StartTripResponse> StartAsync(string userId, StartTripRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        await EnsureOnboardingReadyAsync(user.Id, cancellationToken);
        DriverVehicle vehicle = await GetOwnedCompletedVehicleAsync(user.Id, NormalizeRequired(request.VehicleId), cancellationToken);
        UserDevice mobile = await GetOwnedLinkedDeviceAsync(user.Id, NormalizeRequired(request.MobileDeviceId), DeviceType.MobileApp, cancellationToken);
        UserDevice? smartwatch = null;

        if (!string.IsNullOrWhiteSpace(request.SmartwatchDeviceId))
        {
            smartwatch = await GetOwnedLinkedDeviceAsync(user.Id, NormalizeRequired(request.SmartwatchDeviceId), DeviceType.Smartwatch, cancellationToken);
            if (smartwatch.ParentDeviceId != mobile.Id)
            {
                throw new NotFoundAppException("Smartwatch was not found.");
            }
        }

        Trip? activeTrip = await _trips.GetActiveByUserIdAsync(user.Id, cancellationToken);
        if (activeTrip is not null)
        {
            if (IsSameStart(activeTrip, vehicle.Id, mobile.Id))
            {
                return new StartTripResponse(ToResponse(activeTrip));
            }

            throw new ActiveTripExistsAppException("An active trip already exists for this user.");
        }

        DateTimeOffset now = _clock.UtcNow;
        var trip = new Trip
        {
            UserId = user.Id,
            VehicleId = vehicle.Id,
            MobileDeviceId = mobile.Id,
            SmartwatchDeviceId = smartwatch?.Id,
            Status = TripStatus.Active,
            StartedAtUtc = now,
            ClientStartedAtUtc = request.ClientStartedAtUtc,
            StartLocation = ToLocation(request.StartLocation),
            StartBatteryLevel = request.BatteryLevel,
            AppVersion = NormalizeOptional(request.AppVersion),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _trips.AddAsync(trip, cancellationToken);
        return new StartTripResponse(ToResponse(trip));
    }

    public async Task<FinishTripResponse> FinishAsync(string userId, string tripId, FinishTripRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        Trip trip = await GetOwnedTripAsync(user.Id, tripId, cancellationToken);

        if (trip.Status == TripStatus.Finished)
        {
            return new FinishTripResponse(ToResponse(trip));
        }

        DateTimeOffset now = _clock.UtcNow;
        trip.Status = TripStatus.Finished;
        trip.FinishedAtUtc = now;
        trip.ClientFinishedAtUtc = request.ClientFinishedAtUtc;
        trip.EndLocation = ToLocation(request.EndLocation);
        trip.EndBatteryLevel = request.BatteryLevel;
        trip.Notes = NormalizeOptional(request.Notes);
        trip.UpdatedAtUtc = now;

        await _trips.UpdateAsync(trip, cancellationToken);
        return new FinishTripResponse(ToResponse(trip));
    }

    public async Task<GetTripResponse> GetAsync(string userId, string tripId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        Trip trip = await GetOwnedTripAsync(user.Id, tripId, cancellationToken);
        return new GetTripResponse(ToResponse(trip));
    }

    public async Task<GetTripsResponse> ListAsync(string userId, string? status, int? pageNumber, int? pageSize, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        TripStatus? parsedStatus = ParseEnum<TripStatus>(status);
        int normalizedPageNumber = Math.Max(pageNumber ?? DefaultPageNumber, 1);
        int normalizedPageSize = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        IReadOnlyList<Trip> trips = await _trips.ListByUserIdAsync(user.Id, parsedStatus, normalizedPageNumber, normalizedPageSize, cancellationToken);
        long totalCount = await _trips.CountByUserIdAsync(user.Id, parsedStatus, cancellationToken);

        return new GetTripsResponse(trips.Select(ToResponse).ToArray(), normalizedPageNumber, normalizedPageSize, totalCount);
    }

    private async Task<User> GetRiderUserAsync(string userId, CancellationToken cancellationToken)
    {
        User? user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAppException("Invalid authentication credentials.");
        }

        if (user.Role != UserRole.Rider)
        {
            throw new ForbiddenAppException("Trips API is available only for riders.");
        }

        return user;
    }

    private async Task EnsureOnboardingReadyAsync(string userId, CancellationToken cancellationToken)
    {
        OnboardingStatusResponse status = await _onboarding.GetStatusAsync(userId, cancellationToken);
        if (status.CompletedSteps != 7 || status.CurrentStep != "Completed" || !status.IsOperational)
        {
            throw new OnboardingNotReadyAppException("Onboarding must be completed before starting trips.");
        }
    }

    private async Task<DriverVehicle> GetOwnedCompletedVehicleAsync(string userId, string vehicleId, CancellationToken cancellationToken)
    {
        DriverVehicle? vehicle = await _vehicles.GetByIdAsync(vehicleId, cancellationToken);
        if (vehicle is null || vehicle.UserId != userId || !vehicle.IsActive || vehicle.CompletionStatus != VehicleCompletionStatus.Completed)
        {
            throw new TripNotReadyAppException("Vehicle is not ready for trips.");
        }

        return vehicle;
    }

    private async Task<UserDevice> GetOwnedLinkedDeviceAsync(string userId, string deviceId, DeviceType deviceType, CancellationToken cancellationToken)
    {
        UserDevice? device = await _devices.GetByIdAsync(deviceId, cancellationToken);
        if (device is null || device.UserId != userId || !device.IsActive || device.DeviceType != deviceType || device.LinkStatus != DeviceLinkStatus.Linked)
        {
            throw new TripNotReadyAppException("Device is not ready for trips.");
        }

        return device;
    }

    private async Task<Trip> GetOwnedTripAsync(string userId, string tripId, CancellationToken cancellationToken)
    {
        Trip? trip = await _trips.GetByIdAsync(tripId, cancellationToken);
        if (trip is null || trip.UserId != userId)
        {
            throw new NotFoundAppException("Trip was not found.");
        }

        return trip;
    }

    private static bool IsSameStart(Trip trip, string vehicleId, string mobileDeviceId) =>
        trip.VehicleId == vehicleId && trip.MobileDeviceId == mobileDeviceId;

    private static TripResponse ToResponse(Trip trip) => new(
        trip.Id,
        trip.UserId,
        trip.VehicleId,
        trip.MobileDeviceId,
        trip.SmartwatchDeviceId,
        trip.Status.ToString(),
        trip.StartedAtUtc,
        trip.FinishedAtUtc,
        trip.ClientStartedAtUtc,
        trip.ClientFinishedAtUtc,
        ToLocationResponse(trip.StartLocation),
        ToLocationResponse(trip.EndLocation),
        trip.StartBatteryLevel,
        trip.EndBatteryLevel,
        trip.AppVersion,
        trip.Notes,
        trip.CreatedAtUtc,
        trip.UpdatedAtUtc);

    private static TripLocation? ToLocation(TripLocationRequest? request) => request is null
        ? null
        : new TripLocation
        {
            Latitude = request.Latitude!.Value,
            Longitude = request.Longitude!.Value,
            AccuracyMeters = request.AccuracyMeters,
            Provider = NormalizeOptional(request.Provider),
            RecordedAtUtc = request.RecordedAtUtc
        };

    private static TripLocationResponse? ToLocationResponse(TripLocation? location) => location is null
        ? null
        : new TripLocationResponse(location.Latitude, location.Longitude, location.AccuracyMeters, location.Provider, location.RecordedAtUtc);

    private static TEnum? ParseEnum<TEnum>(string? value)
        where TEnum : struct => Enum.TryParse(value, ignoreCase: true, out TEnum result) ? result : null;

    private static string NormalizeRequired(string? value) => value?.Trim() ?? string.Empty;
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
