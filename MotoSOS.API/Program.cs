using MotoSOS.API.Configuration.DependencyInjection;
using MotoSOS.API.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiConfiguration()
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration)
    .AddSecurityServices();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseApiMiddleware();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/ready", () => Results.Ok(new { status = "Ready" }));

app.Run();

public partial class Program;
