using MotoSOS.API.Configuration.DependencyInjection;
using MotoSOS.API.Middleware;
using MotoSOS.API.Modules.Auth.Endpoints;
using MotoSOS.API.Modules.Users.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiConfiguration()
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration)
    .AddSecurityServices(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseApiMiddleware();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "Ready" }));
app.MapAuthEndpoints();
app.MapUserEndpoints();

app.Run();

public partial class Program;
