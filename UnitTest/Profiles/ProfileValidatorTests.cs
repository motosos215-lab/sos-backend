using FluentAssertions;
using MotoSOS.API.Modules.Profiles.Application;
using MotoSOS.API.Modules.Profiles.Contracts;

namespace UnitTest.Profiles;

public sealed class ProfileValidatorTests
{
    [Fact]
    public void DraftAllowsPartialData()
    {
        var validator = new UpsertMyProfileRequestValidator();
        var request = new UpsertMyProfileRequest(null, null, null, null, null, null, null, null, null, null, null, "Draft");

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ContinueRequiresMinimumFields()
    {
        var validator = new UpsertMyProfileRequestValidator();
        var request = new UpsertMyProfileRequest(null, null, null, null, null, null, null, null, null, null, null, "Continue");

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpsertMyProfileRequest.FullName));
        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpsertMyProfileRequest.PhoneNumber));
        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpsertMyProfileRequest.DateOfBirth));
    }

    [Fact]
    public void InvalidPhoneFails()
    {
        var validator = new UpsertMyProfileRequestValidator();
        var request = ValidContinueRequest() with { PhoneNumber = "bad-phone" };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void InvalidSaveModeFails()
    {
        var validator = new UpsertMyProfileRequestValidator();
        var request = ValidContinueRequest() with { SaveMode = "Publish" };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(UpsertMyProfileRequest.SaveMode));
    }

    [Fact]
    public void ContinueWithMinimumFieldsPasses()
    {
        var validator = new UpsertMyProfileRequestValidator();

        var result = validator.Validate(ValidContinueRequest());

        result.IsValid.Should().BeTrue();
    }

    private static UpsertMyProfileRequest ValidContinueRequest() => new(
        "Moto Rider",
        "+52 555 555 5555",
        new DateOnly(1995, 1, 15),
        null,
        "Colonia Centro",
        "Toluca",
        null,
        null,
        null,
        "Contacto Principal",
        "+52 555 111 2233",
        "Continue");
}
