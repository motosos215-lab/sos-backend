using FluentAssertions;
using MotoSOS.API.Security.Hashing;

namespace UnitTest.Security;

public sealed class PasswordHasherTests
{
    [Fact]
    public void HashCreatesVerifiablePasswordHash()
    {
        var hasher = new PasswordHasher();

        string passwordHash = hasher.Hash("StrongPass1!");

        passwordHash.Should().NotBe("StrongPass1!");
        hasher.Verify("StrongPass1!", passwordHash).Should().BeTrue();
    }

    [Fact]
    public void VerifyRejectsIncorrectPassword()
    {
        var hasher = new PasswordHasher();

        string passwordHash = hasher.Hash("StrongPass1!");

        hasher.Verify("WrongPass1!", passwordHash).Should().BeFalse();
    }
}
