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

            var response = ApiResponse<object>.Fail(ex.Message);
            await context.Response.WriteAsJsonAsync(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred during request execution");

            var sqlState = ex.GetType().GetProperty("SqlState")?.GetValue(ex)?.ToString();
            var isUniqueConstraintViolation = sqlState == "23505";

            context.Response.StatusCode = isUniqueConstraintViolation 
                ? StatusCodes.Status409Conflict 
                : StatusCodes.Status500InternalServerError;

            context.Response.ContentType = "application/json";

            var errorMessage = isUniqueConstraintViolation
                ? "Resource already exists"
                : $"Internal server error: {ex.Message}";

            var response = ApiResponse<object>.Fail(errorMessage);
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
