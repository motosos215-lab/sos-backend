using FluentAssertions;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Modules.AuditLogRetention.Application;

namespace UnitTest.AuditLogRetention;

public sealed class AuditLogRetentionRunQueryValidatorTests
{
    [Fact]
    public void NullPagingUsesDefaults()
    {
        AuditLogRetentionRunQuery query = new AuditLogRetentionRunQueryValidator().Validate(null, null);

        query.PageNumber.Should().Be(1);
        query.PageSize.Should().Be(50);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void InvalidPagingThrowsValidationError(int pageNumber, int pageSize)
    {
        Action act = () => new AuditLogRetentionRunQueryValidator().Validate(pageNumber, pageSize);

        act.Should().Throw<ValidationAppException>();
    }
}
