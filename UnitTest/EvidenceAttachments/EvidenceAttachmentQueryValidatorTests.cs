using FluentAssertions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.EvidenceAttachments.Application;

namespace UnitTest.EvidenceAttachments;

public sealed class EvidenceAttachmentQueryValidatorTests
{
    private readonly EvidenceAttachmentQueryValidator _validator = new();

    [Fact]
    public void EmptyQueryIsValidAndPaginationDefaults()
    {
        EvidenceAttachmentQuery query = _validator.Validate(null, null, null, null, null, null, null, null, null, null, null);
        query.PageNumber.Should().Be(1);
        query.PageSize.Should().Be(20);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void InvalidPaginationThrows(int pageNumber, int pageSize) => Assert.Throws<ValidationAppException>(() => _validator.Validate(null, null, null, null, null, null, null, null, null, pageNumber, pageSize));

    [Fact]
    public void ValidatesDatesAndEnums()
    {
        _validator.Validate(null, null, null, null, "Photo", "RiderMobileApp", "Registered", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow, 1, 20).EvidenceType.Should().NotBeNull();
        Assert.Throws<ValidationAppException>(() => _validator.Validate(null, null, null, null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(-1), 1, 20));
        Assert.Throws<ValidationAppException>(() => _validator.Validate(null, null, null, null, "Nope", null, null, null, null, 1, 20));
        Assert.Throws<ValidationAppException>(() => _validator.Validate(null, null, null, null, null, "Nope", null, null, null, 1, 20));
        Assert.Throws<ValidationAppException>(() => _validator.Validate(null, null, null, null, null, null, "Nope", null, null, 1, 20));
    }
}
