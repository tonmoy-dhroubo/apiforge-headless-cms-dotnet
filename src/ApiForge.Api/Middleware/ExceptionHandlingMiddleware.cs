using ApiForge.Core;
using ApiForge.Infrastructure;

namespace ApiForge.Api.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiForgeException ex)
        {
            context.Response.StatusCode = ex.Status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred");
            var state = ex.GetType().GetProperty("SqlState")?.GetValue(ex)?.ToString();
            context.Response.StatusCode = state == "23505" ? 409 : 500;
            context.Response.ContentType = "application/json";
            var message = state == "23505" ? "Resource already exists" : $"Internal server error: {ex.Message}";
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message));
        }
    }
}
