namespace MotoSOS.API.Common.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
