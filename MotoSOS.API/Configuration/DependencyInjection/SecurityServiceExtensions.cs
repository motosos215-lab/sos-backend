using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using MotoSOS.API.Common.Results;
using MotoSOS.API.Modules.Auth.Sessions.Application;
using MotoSOS.API.Modules.Users.Domain;
using MotoSOS.API.Security.Hashing;
using MotoSOS.API.Security.Tokens;

namespace MotoSOS.API.Configuration.DependencyInjection;

public static class SecurityServiceExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IRefreshTokenGenerator, RefreshTokenGenerator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                JwtOptions jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
                byte[] signingKey = Encoding.UTF8.GetBytes(jwtOptions.Key ?? string.Empty);

                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey.Length > 0 ? new SymmetricSecurityKey(signingKey) : null,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        string? role = context.Principal?.FindFirstValue(ClaimTypes.Role);
                        if (!Enum.TryParse(role, ignoreCase: true, out UserRole parsedRole))
                        {
                            context.Fail("session_revoked");
                            return;
                        }

                        string? userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Principal?.FindFirstValue("sub");
                        string? sessionId = context.Principal?.FindFirstValue("sid");
                        if (parsedRole == UserRole.Admin && string.IsNullOrWhiteSpace(sessionId))
                        {
                            return;
                        }

                        if (parsedRole is not (UserRole.Rider or UserRole.Monitor or UserRole.Admin) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
                        {
                            context.Fail("session_revoked");
                            return;
                        }

                        IUserSessionRepository sessions = context.HttpContext.RequestServices.GetRequiredService<IUserSessionRepository>();
                        var session = await sessions.GetByIdAsync(sessionId, context.HttpContext.RequestAborted);
                        if (session is null || session.UserId != userId || session.RevokedAtUtc is not null)
                        {
                            context.Fail("session_revoked");
                        }
                    },
                    OnChallenge = async context =>
                    {
                        if (context.Response.HasStarted)
                        {
                            return;
                        }

                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        var response = ApiResponse<object>.Fail(new ApiError("session_revoked", "Session has been revoked."));
                        await JsonSerializer.SerializeAsync(context.Response.Body, response, JsonOptions, context.HttpContext.RequestAborted);
                    }
                };
            });

        services.AddAuthorization();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                PathString path = httpContext.Request.Path;

                if (path.StartsWithSegments("/health"))
                {
                    return RateLimitPartition.GetNoLimiter("health");
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    "global",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 100,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1)
                    });
            });

            options.AddPolicy("AuthRateLimit", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 10,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });

        return services;
    }
}
