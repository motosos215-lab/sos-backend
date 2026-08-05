using MotoSOS.API.Common.Abstractions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.Users.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Modules.Vehicles.Contracts;
using MotoSOS.API.Modules.Vehicles.Domain;

namespace MotoSOS.API.Modules.Vehicles.Application;

public sealed class VehicleService : IVehicleService
{
    private readonly IUserRepository _users;
    private readonly IDriverVehicleRepository _vehicles;
    private readonly IClock _clock;

    public VehicleService(IUserRepository users, IDriverVehicleRepository vehicles, IClock clock)
    {
        _users = users;
        _vehicles = vehicles;
        _clock = clock;
    }

    public async Task<GetVehiclesResponse> GetMyVehiclesAsync(string userId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        IReadOnlyList<DriverVehicle> vehicles = await _vehicles.GetActiveByUserIdAsync(user.Id, cancellationToken);

        return new GetVehiclesResponse(vehicles.Select(ToResponse).ToArray());
    }

    public async Task<GetVehicleResponse> GetMyVehicleAsync(string userId, string vehicleId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        DriverVehicle vehicle = await GetOwnedActiveVehicleAsync(user.Id, vehicleId, cancellationToken);

        return new GetVehicleResponse(ToResponse(vehicle));
    }

    public async Task<CreateVehicleResponse> CreateMyVehicleAsync(string userId, CreateVehicleRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        int activeVehicles = await _vehicles.CountActiveByUserIdAsync(user.Id, cancellationToken);

        if (activeVehicles >= 1)
        {
            throw new PlanLimitExceededAppException("Basic plan allows only one active vehicle.");
        }

        DateTimeOffset now = _clock.UtcNow;
        var vehicle = new DriverVehicle
        {
            UserId = user.Id,
            IsActive = true,
            IsPrimary = activeVehicles == 0,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        ApplyChanges(vehicle, request);
        ApplyCompletion(vehicle, request.SaveMode, now);

        await _vehicles.AddAsync(vehicle, cancellationToken);

        return new CreateVehicleResponse(ToResponse(vehicle));
    }

    public async Task<UpdateVehicleResponse> UpdateMyVehicleAsync(string userId, string vehicleId, UpdateVehicleRequest request, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        DriverVehicle vehicle = await GetOwnedActiveVehicleAsync(user.Id, vehicleId, cancellationToken);
        DateTimeOffset now = _clock.UtcNow;

        ApplyChanges(vehicle, request);
        ApplyCompletion(vehicle, request.SaveMode, now);
        vehicle.UpdatedAtUtc = now;

        await _vehicles.UpdateAsync(vehicle, cancellationToken);

        return new UpdateVehicleResponse(ToResponse(vehicle));
    }

    public async Task DeleteMyVehicleAsync(string userId, string vehicleId, CancellationToken cancellationToken)
    {
        User user = await GetRiderUserAsync(userId, cancellationToken);
        DriverVehicle vehicle = await GetOwnedActiveVehicleAsync(user.Id, vehicleId, cancellationToken);

        vehicle.IsActive = false;
        vehicle.UpdatedAtUtc = _clock.UtcNow;
        await _vehicles.UpdateAsync(vehicle, cancellationToken);
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
            throw new ForbiddenAppException("This vehicles flow is available only for riders.");
        }

        return user;
    }

    private async Task<DriverVehicle> GetOwnedActiveVehicleAsync(string userId, string vehicleId, CancellationToken cancellationToken)
    {
        DriverVehicle? vehicle = await _vehicles.GetByIdAsync(vehicleId, cancellationToken);

        if (vehicle is null || !vehicle.IsActive || vehicle.UserId != userId)
        {
            throw new NotFoundAppException("Vehicle was not found.");
        }

        return vehicle;
    }

    private static void ApplyChanges(DriverVehicle vehicle, CreateVehicleRequest request)
    {
        vehicle.VehicleType = ParseEnum<VehicleType>(request.VehicleType);
        vehicle.Brand = NormalizeOptional(request.Brand);
        vehicle.Model = NormalizeOptional(request.Model);
        vehicle.Year = request.Year;
        vehicle.Alias = NormalizeOptional(request.Alias);
        vehicle.PrimaryUse = ParseEnum<VehiclePrimaryUse>(request.PrimaryUse);
        vehicle.Color = NormalizeOptional(request.Color);
        vehicle.PlateNumber = NormalizeOptional(request.PlateNumber);
        vehicle.Vin = NormalizeOptional(request.Vin);
        vehicle.UsageFrequency = ParseEnum<VehicleUsageFrequency>(request.UsageFrequency);
    }

    private static void ApplyChanges(DriverVehicle vehicle, UpdateVehicleRequest request)
    {
        vehicle.VehicleType = ParseEnum<VehicleType>(request.VehicleType);
        vehicle.Brand = NormalizeOptional(request.Brand);
        vehicle.Model = NormalizeOptional(request.Model);
        vehicle.Year = request.Year;
        vehicle.Alias = NormalizeOptional(request.Alias);
        vehicle.PrimaryUse = ParseEnum<VehiclePrimaryUse>(request.PrimaryUse);
        vehicle.Color = NormalizeOptional(request.Color);
        vehicle.PlateNumber = NormalizeOptional(request.PlateNumber);
        vehicle.Vin = NormalizeOptional(request.Vin);
        vehicle.UsageFrequency = ParseEnum<VehicleUsageFrequency>(request.UsageFrequency);
    }

    private static void ApplyCompletion(DriverVehicle vehicle, string? saveMode, DateTimeOffset now)
    {
        if (string.Equals(saveMode?.Trim(), nameof(SaveMode.Continue), StringComparison.OrdinalIgnoreCase))
        {
            vehicle.CompletionStatus = VehicleCompletionStatus.Completed;
            vehicle.CompletedAtUtc ??= now;
            return;
        }

        vehicle.CompletionStatus = VehicleCompletionStatus.Draft;
        vehicle.CompletedAtUtc = null;
    }

    private static VehicleResponse ToResponse(DriverVehicle vehicle) => new(
        vehicle.Id,
        vehicle.UserId,
        vehicle.VehicleType?.ToString(),
        vehicle.Brand,
        vehicle.Model,
        vehicle.Year,
        vehicle.Alias,
        vehicle.PrimaryUse?.ToString(),
        vehicle.Color,
        vehicle.PlateNumber,
        vehicle.Vin,
        vehicle.UsageFrequency?.ToString(),
        vehicle.CompletionStatus.ToString(),
        vehicle.IsPrimary,
        vehicle.IsActive,
        vehicle.CreatedAtUtc,
        vehicle.UpdatedAtUtc,
        vehicle.CompletedAtUtc);

    private static TEnum? ParseEnum<TEnum>(string? value)
        where TEnum : struct
    {
        return Enum.TryParse(value, ignoreCase: true, out TEnum result) ? result : null;
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
