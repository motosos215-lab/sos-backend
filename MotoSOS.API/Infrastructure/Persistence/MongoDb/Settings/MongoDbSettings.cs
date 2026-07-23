using System.ComponentModel.DataAnnotations;

namespace MotoSOS.API.Infrastructure.Persistence.MongoDb.Settings;

public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Required]
    public string DatabaseName { get; init; } = string.Empty;
}
