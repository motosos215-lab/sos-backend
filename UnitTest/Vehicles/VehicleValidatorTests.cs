using FluentAssertions;
using MotoSOS.API.Modules.Vehicles.Application;
using MotoSOS.API.Modules.Vehicles.Contracts;

namespace UnitTest.Vehicles;

public sealed class VehicleValidatorTests
{
    [Fact]
    public void DraftAllowsPartialData()
    {
        var validator = new CreateVehicleRequestValidator();
        var request = new CreateVehicleRequest(null, null, null, null, null, null, null, null, null, null, "Draft");

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ContinueRequiresMinimumFields()
    {
        var validator = new CreateVehicleRequestValidator();
        var request = new CreateVehicleRequest(null, null, null, null, null, null, null, null, null, null, "Continue");

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateVehicleRequest.VehicleType));
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateVehicleRequest.Brand));
        result.Errors.Should().Contain(error => error.PropertyName == nameof(CreateVehicleRequest.Year));
    }

    [Fact]
    public void YearBefore1950Fails()
    {
        var validator = new CreateVehicleRequestValidator();
        var request = ValidContinueRequest() with { Year = 1949 };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void YearAfterNextYearFails()
    {
        var validator = new CreateVehicleRequestValidator();
        var request = ValidContinueRequest() with { Year = DateTime.UtcNow.Year + 2 };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("VehicleType")]
    [InlineData("PrimaryUse")]
    [InlineData("UsageFrequency")]
    [InlineData("SaveMode")]
    public void InvalidEnumLikeValuesFail(string field)
    {
        var validator = new CreateVehicleRequestValidator();
        CreateVehicleRequest request = field switch
        {
            "VehicleType" => ValidContinueRequest() with { VehicleType = "Car" },
            "PrimaryUse" => ValidContinueRequest() with { PrimaryUse = "Racing" },
            "UsageFrequency" => ValidContinueRequest() with { UsageFrequency = "Hourly" },
            _ => ValidContinueRequest() with { SaveMode = "Publish" }
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void PlateNumberTooLongFails()
    {
        var validator = new CreateVehicleRequestValidator();
        var request = ValidContinueRequest() with { PlateNumber = new string('A', 21) };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void VinTooLongFails()
    {
        var validator = new CreateVehicleRequestValidator();
        var request = ValidContinueRequest() with { Vin = new string('V', 41) };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ContinueValidRequestPasses()
    {
        var validator = new CreateVehicleRequestValidator();

        var result = validator.Validate(ValidContinueRequest());

        result.IsValid.Should().BeTrue();
    }

    private static CreateVehicleRequest ValidContinueRequest() => new(
        "Motorcycle",
        "Yamaha",
        "FZ 2.0",
        2022,
        "Mi moto",
        "Personal",
        "Rojo",
        "ABC1234",
        "VIN123456789",
        "Daily",
        "Continue");
}
