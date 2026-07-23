using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using MotoSOS.API.Middleware.ExceptionHandling;

namespace SecurityTest;

public sealed class GlobalExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task MiddlewareReturnsGenericErrorInProduction()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new GlobalExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("internal implementation detail"),
            NullLogger<GlobalExceptionHandlingMiddleware>.Instance,
            new TestHostEnvironment("Production"));

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        context.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        body.Should().Contain("internal_error");
        body.Should().NotContain("internal implementation detail");
        body.Should().NotContain("InvalidOperationException");
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "MotoSOS.API.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
