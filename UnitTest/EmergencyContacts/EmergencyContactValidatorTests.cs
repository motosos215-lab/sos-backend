using FluentAssertions;
using MotoSOS.API.Modules.EmergencyContacts.Application;
using MotoSOS.API.Modules.EmergencyContacts.Contracts;

namespace UnitTest.EmergencyContacts;

public sealed class EmergencyContactValidatorTests
{
    [Fact]
    public void DraftAllowsPartialData()
    {
        var validator = new CreateEmergencyContactRequestValidator();
        var request = new CreateEmergencyContactRequest(null, null, null, null, null, null, "Draft");

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ContinueRequiresMinimumFields()
    {
        var validator = new CreateEmergencyContactRequestValidator();
        var request = new CreateEmergencyContactRequest(null, null, null, null, null, null, "Continue");

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateEmergencyContactRequest.FullName));
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateEmergencyContactRequest.Email));
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateEmergencyContactRequest.Priority));
    }

    [Theory]
    [InlineData("bad-email", "+52 5512345678", 1, "Continue")]
    [InlineData("maria@example.com", "bad-phone", 1, "Continue")]
    [InlineData("maria@example.com", "+52 5512345678", 0, "Continue")]
    [InlineData("maria@example.com", "+52 5512345678", 1, "Publish")]
    public void InvalidFieldsFail(string email, string phoneNumber, int priority, string saveMode)
    {
        var validator = new CreateEmergencyContactRequestValidator();
        var request = ValidContinueRequest() with { Email = email, PhoneNumber = phoneNumber, Priority = priority, SaveMode = saveMode };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ContinueValidRequestPasses()
    {
        var validator = new CreateEmergencyContactRequestValidator();

        var result = validator.Validate(ValidContinueRequest());

        result.IsValid.Should().BeTrue();
    }

    private static CreateEmergencyContactRequest ValidContinueRequest() => new(
        "Maria Lopez",
        "Esposa",
        "+52 5512345678",
        "maria@example.com",
        1,
        new EmergencyContactPermissionsRequest(true, true, false, false),
        "Continue");
}
