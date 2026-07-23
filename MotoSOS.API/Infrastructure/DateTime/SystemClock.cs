using MotoSOS.API.Common.Abstractions;

namespace MotoSOS.API.Infrastructure.DateTime;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
