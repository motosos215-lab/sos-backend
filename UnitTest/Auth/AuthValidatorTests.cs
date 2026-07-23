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
        var request = new RegisterRequest("rider@example.com", "StrongPass1!", "StrongPass1!", "Moto Rider", "+52 555 555 5555", "Rider", true);

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RegisterValidatorRejectsWeakPassword()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("rider@example.com", "password", "password", "Moto Rider", null, "Rider", true);

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RegisterValidatorRejectsMismatchedConfirmPassword()
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("rider@example.com", "StrongPass1!", "Different1!", "Moto Rider", null, "Rider", true);

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("Rider")]
    [InlineData("Conductor")]
    [InlineData("Monitor")]
    public void RegisterValidatorAcceptsPublicAccountTypes(string accountType)
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("rider@example.com", "StrongPass1!", "StrongPass1!", "Moto Rider", null, accountType, true);

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Administrator")]
    [InlineData("Administrador")]
    public void RegisterValidatorRejectsAdminAccountTypes(string accountType)
    {
        var validator = new RegisterRequestValidator();
        var request = new RegisterRequest("rider@example.com", "StrongPass1!", "StrongPass1!", "Moto Rider", null, accountType, true);

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
