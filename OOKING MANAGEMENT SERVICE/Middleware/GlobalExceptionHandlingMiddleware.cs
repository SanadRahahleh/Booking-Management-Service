using System.Net;
using System.Text.Json;

namespace BookingManagementService.Middleware;

public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred during request processing.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message) = exception switch
        {
            ArgumentException argEx => ((int)HttpStatusCode.BadRequest, argEx.Message),
            KeyNotFoundException keyEx => ((int)HttpStatusCode.NotFound, keyEx.Message),
            InvalidOperationException invEx => ((int)HttpStatusCode.Conflict, invEx.Message),
            _ => ((int)HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later.")
        };

        context.Response.StatusCode = statusCode;

        var response = JsonSerializer.Serialize(new { message });
        return context.Response.WriteAsync(response);
    }
}
