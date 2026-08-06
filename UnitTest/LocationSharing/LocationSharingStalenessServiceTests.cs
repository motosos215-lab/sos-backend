using FluentAssertions;
using MotoSOS.API.Modules.LocationSharing.Application;

namespace UnitTest.LocationSharing;

public sealed class LocationSharingStalenessServiceTests
{
    [Fact]
    public void FiveMinutesOrLessIsNotStaleAndOlderIsStale()
    {
        var service = new LocationSharingStalenessService();
        DateTimeOffset now = new(2026, 8, 6, 14, 20, 0, TimeSpan.Zero);
        service.IsStale(now.AddMinutes(-5), now).Should().BeFalse();
        service.IsStale(now.AddMinutes(-5).AddTicks(-1), now).Should().BeTrue();
    }
}
