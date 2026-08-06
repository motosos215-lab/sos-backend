using FluentAssertions;
using MotoSOS.API.Modules.Devices.Application;

namespace UnitTest.Devices;

public sealed class ActivationCodeGeneratorTests
{
    [Fact]
    public void CreateCodeReturnsExpectedFormat()
    {
        var generator = new ActivationCodeGenerator();

        string code = generator.CreateCode();

        code.Should().MatchRegex("^MSOS-[A-Z2-9]{4}-[A-Z2-9]{4}$");
        code.Should().NotContain("@");
    }

    [Fact]
    public void ConsecutiveCodesAreDifferent()
    {
        var generator = new ActivationCodeGenerator();

        generator.CreateCode().Should().NotBe(generator.CreateCode());
    }
}
