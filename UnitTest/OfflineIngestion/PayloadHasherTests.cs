using FluentAssertions;
using MotoSOS.API.Modules.OfflineIngestion.Application;

namespace UnitTest.OfflineIngestion;

public sealed class PayloadHasherTests
{
    [Fact]
    public void HashIsStableAndDoesNotReturnPayload()
    {
        var hasher = new PayloadHasher();

        string first = hasher.Hash("{\"score\":87}");
        string same = hasher.Hash("{\"score\":87}");
        string different = hasher.Hash("{\"score\":88}");

        same.Should().Be(first);
        different.Should().NotBe(first);
        first.Should().NotContain("score");
    }
}
