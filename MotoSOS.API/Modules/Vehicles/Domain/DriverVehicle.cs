using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MotoSOS.API.Modules.Vehicles.Domain;

public sealed class DriverVehicle
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    public string UserId { get; set; } = string.Empty;

    public VehicleType? VehicleType { get; set; }

    public string? Brand { get; set; }

    public string? Model { get; set; }

    public int? Year { get; set; }

    public string? Alias { get; set; }

    public VehiclePrimaryUse? PrimaryUse { get; set; }

    public string? Color { get; set; }

    public string? PlateNumber { get; set; }

    public string? Vin { get; set; }

    public VehicleUsageFrequency? UsageFrequency { get; set; }

    public VehicleCompletionStatus CompletionStatus { get; set; } = VehicleCompletionStatus.Draft;

    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
