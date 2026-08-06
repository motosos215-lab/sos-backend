using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SecurityTest;

public sealed class CorsPolicyTests
{
    [Fact]
    public async Task PreflightFromAllowedOriginReturnsCorsHeaders()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        HttpClient client = factory.CreateClient();
        using var request = CreatePreflightRequest("http://localhost:5173");

        HttpResponseMessage response = await client.SendAsync(request);

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("http://localhost:5173");
        response.Headers.GetValues("Access-Control-Allow-Methods").Should().Contain(methods => methods.Contains("POST", StringComparison.Ordinal));
        response.Headers.GetValues("Access-Control-Allow-Headers").Should().Contain(headers => headers.Contains("Authorization", StringComparison.OrdinalIgnoreCase));
        response.Headers.GetValues("Access-Control-Allow-Headers").Should().Contain(headers => headers.Contains("Content-Type", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PreflightFromDisallowedOriginDoesNotReturnAllowOriginHeader()
    {
        await using WebApplicationFactory<Program> factory = CreateFactory();
        HttpClient client = factory.CreateClient();
        using var request = CreatePreflightRequest("https://evil.example");

        HttpResponseMessage response = await client.SendAsync(request);

        response.Headers.Should().NotContainKey("Access-Control-Allow-Origin");
    }

    private static HttpRequestMessage CreatePreflightRequest(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Authorization, Content-Type");

        return request;
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "MotoSOS",
                    ["Jwt:Audience"] = "MotoSOS.Clients",
                    ["Jwt:Key"] = new string('C', 48),
                    ["Jwt:AccessTokenMinutes"] = "15",
                    ["Jwt:RefreshTokenDays"] = "7",
                    ["Jwt:RefreshTokenRememberMeDays"] = "30",
                    ["MongoDb:ConnectionString"] = string.Empty,
                    ["MongoDb:DatabaseName"] = "MotoSOS_Test",
                    ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                    ["Cors:AllowedOrigins:1"] = "http://localhost:3000"
                });
            });
        });
    }
}
