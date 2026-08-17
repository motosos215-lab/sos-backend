using FluentAssertions;
using MotoSOS.API.Modules.Trips.Contracts;
using MotoSOS.API.Modules.Trips.Domain;

namespace SecurityTest;

public sealed class TripRoutePointSecurityTests
{
    [Fact]
    public void TripRoutePointContractsDoNotExposeUnsupportedMapOrRealtimeFeatures()
    {
        Type[] types = [typeof(CreateTripRoutePointsBatchRequest), typeof(CreateTripRoutePointsBatchResponse), typeof(GetTripRouteResponse), typeof(TripRoutePointResponse), typeof(TripRoutePoint)];
        string joinedNames = string.Join(' ', types.Select(type => type.FullName).Concat(types.SelectMany(type => type.GetProperties().Select(property => property.Name)))).ToLowerInvariant();

        joinedNames.Should().NotContain("googlemaps");
        joinedNames.Should().NotContain("directions");
        joinedNames.Should().NotContain("encodedpolyline");
        joinedNames.Should().NotContain("apikey");
        joinedNames.Should().NotContain("secret");
        joinedNames.Should().NotContain("payload");
        joinedNames.Should().NotContain("stacktrace");
        joinedNames.Should().NotContain(("signal" + "r").ToLowerInvariant());
        joinedNames.Should().NotContain(("web" + "socket").ToLowerInvariant());
    }
}
