using System.Text.Json;
using MotoSOS.API.Common.Exceptions;
using MotoSOS.API.Common.Results;

namespace MotoSOS.API.Middleware.ExceptionHandling;

public sealed class GlobalExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        int statusCode = StatusCodes.Status500InternalServerError;
        string code = "internal_error";
        string message = "An unexpected error occurred.";

        if (exception is AppException appException)
        {
            statusCode = appException.StatusCode;
            code = appException.Code;
            message = appException.Message;
        }
        else if (_environment.IsDevelopment())
        {
            message = exception.Message;
        }

        _logger.LogError(
            "Unhandled request exception. Method: {Method}. Path: {Path}. StatusCode: {StatusCode}. ExceptionType: {ExceptionType}.",
            context.Request.Method,
            context.Request.Path.Value,
            statusCode,
            exception.GetType().Name);

        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<object>.Fail(new ApiError(code, message));
        await JsonSerializer.SerializeAsync(context.Response.Body, response, SerializerOptions, context.RequestAborted);
    }
}
