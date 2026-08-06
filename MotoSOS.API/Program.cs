using MongoDB.Bson;
using MongoDB.Driver;
using MotoSOS.API.Configuration.DependencyInjection;
using MotoSOS.API.Middleware;
using MotoSOS.API.Modules.Auth.Endpoints;
using MotoSOS.API.Modules.EmergencyContacts.Endpoints;
using MotoSOS.API.Modules.Onboarding.Endpoints;
using MotoSOS.API.Modules.Profiles.Endpoints;
using MotoSOS.API.Modules.Users.Endpoints;
using MotoSOS.API.Modules.Vehicles.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiConfiguration(builder.Configuration)
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration, builder.Environment)
    .AddSecurityServices(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseApiMiddleware();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/ready", async (IServiceProvider services, IHostEnvironment environment, CancellationToken cancellationToken) =>
{
    IMongoDatabase? database = services.GetService<IMongoDatabase>();

    if (database is null)
    {
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            return Results.Ok(new { status = "Ready", persistence = "NotConfigured" });
        }

        return Results.Json(new { status = "NotReady", persistence = "NotConfigured" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
        return Results.Ok(new { status = "Ready", persistence = "Ready" });
    }
    catch
    {
        return Results.Json(new { status = "NotReady", persistence = "Unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapProfileEndpoints();
app.MapVehicleEndpoints();
app.MapEmergencyContactEndpoints();
app.MapOnboardingEndpoints();

app.Run();

public partial class Program;
