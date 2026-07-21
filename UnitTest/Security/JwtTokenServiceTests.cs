using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MotoSOS.API.Infrastructure.DateTime;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Security.Tokens;

namespace UnitTest.Security;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateAccessTokenReturnsSignedJwt()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "MotoSOS",
            Audience = "MotoSOS.Clients",
            Key = new string('U', 48),
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });
        var service = new JwtTokenService(options, new SystemClock());
        var user = new User
        {
            Id = "user-1",
            Email = "rider@example.com",
            FullName = "Moto Rider",
            Role = UserRole.Rider
        };

        TokenResult result = service.CreateAccessToken(user);

        result.AccessToken.Should().NotBeNullOrWhiteSpace();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        jwt.Claims.Should().Contain(claim => claim.Type == JwtRegisteredClaimNames.Sub && claim.Value == user.Id);
        jwt.Claims.Should().Contain(claim => claim.Type == JwtRegisteredClaimNames.Email && claim.Value == user.Email);
    }
}
