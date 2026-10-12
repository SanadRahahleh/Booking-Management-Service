using System.Net;
using System.Text.Json;
using BookingManagementService.Exceptions;

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
            BookingConflictException conflictEx => ((int)HttpStatusCode.Conflict, conflictEx.Message),
            KeyNotFoundException keyEx => ((int)HttpStatusCode.NotFound, keyEx.Message),
            ArgumentException argEx => ((int)HttpStatusCode.BadRequest, argEx.Message),
            ConcurrencyException => ((int)HttpStatusCode.ServiceUnavailable, "Database is currently busy under high concurrent contention. Please retry your request."),
            _ => ((int)HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later.")
        };

        context.Response.StatusCode = statusCode;

        var response = JsonSerializer.Serialize(new { message });
        return context.Response.WriteAsync(response);
    }
}
