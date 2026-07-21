using FluentAssertions;
using MotoSOS.API.Modules.Auth.Application;
using MotoSOS.API.Modules.Auth.Contracts;

namespace UnitTest.Auth;

public sealed class AuthValidatorTests
{
    [Fact]
    public void RegisterValidatorAcceptsStrongRequest()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("rider@example.com", "StrongPass1!", "Moto Rider", "+52 555 555 5555");

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RegisterValidatorRejectsWeakPassword()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("rider@example.com", "password", "Moto Rider", null);

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void LoginValidatorRejectsMissingPassword()
    {
        var validator = new LoginRequestValidator();
        var request = new LoginRequest("rider@example.com", string.Empty);

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }
}
