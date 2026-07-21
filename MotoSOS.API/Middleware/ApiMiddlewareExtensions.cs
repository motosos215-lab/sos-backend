using MotoSOS.API.Middleware.ExceptionHandling;
using MotoSOS.API.Middleware.RequestLogging;
using MotoSOS.API.Middleware.SecurityHeaders;

namespace MotoSOS.API.Middleware;

public static class ApiMiddlewareExtensions
{
    public static WebApplication UseApiMiddleware(this WebApplication app)
    {
        app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseHttpsRedirection();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }
}
