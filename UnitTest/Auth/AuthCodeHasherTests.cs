using FluentAssertions;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Security.Hashing;

namespace UnitTest.Auth;

public sealed class AuthCodeHasherTests
{
    [Fact]
    public void HashDoesNotReturnPlainCode()
    {
        var hasher = new AuthCodeHasher(new PasswordHasher());

        string hash = hasher.Hash("123456");

        hash.Should().NotBe("123456");
        hasher.Verify("123456", hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyRejectsIncorrectCode()
    {
        var hasher = new AuthCodeHasher(new PasswordHasher());
        string hash = hasher.Hash("123456");

        hasher.Verify("000000", hash).Should().BeFalse();
    }
}
