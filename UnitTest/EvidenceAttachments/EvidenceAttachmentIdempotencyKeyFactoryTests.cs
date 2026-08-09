using FluentAssertions;
using MotoSOS.API.Modules.EvidenceAttachments.Application;

namespace UnitTest.EvidenceAttachments;

public sealed class EvidenceAttachmentIdempotencyKeyFactoryTests
{
    private readonly EvidenceAttachmentIdempotencyKeyFactory _factory = new();

    [Fact]
    public void SameInputProducesSameKeyAndDifferentPartsChangeIt()
    {
        string key = _factory.Create("owner", "client", "Incident", "target");
        key.Should().Be(_factory.Create("owner", "client", "Incident", "target"));
        key.Should().NotBe(_factory.Create("other", "client", "Incident", "target"));
        key.Should().NotBe(_factory.Create("owner", "other", "Incident", "target"));
        key.Should().NotBe(_factory.Create("owner", "client", "AlertDispatch", "target"));
        key.Should().NotBe(_factory.Create("owner", "client", "Incident", "other"));
        key.Should().NotContain("owner").And.NotContain("client").And.HaveLength(64);
    }
}
