using MotoSOS.API.Middleware.ExceptionHandling;
using MotoSOS.API.Middleware.RequestLogging;
using MotoSOS.API.Middleware.SecurityHeaders;

namespace MotoSOS.API.Middleware;

public static class ApiMiddlewareExtensions
{
    public static WebApplication UseApiMiddleware(this WebApplication app)
    {
        if (app.Environment.IsProduction())
        {
            app.UseHsts();
        }

        app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseHttpsRedirection();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }
}
