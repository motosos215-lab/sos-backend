namespace MotoSOS.API.Middleware;

public static class ApiMiddlewareExtensions
{
    public static WebApplication UseApiMiddleware(this WebApplication app)
    {
        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }
}
