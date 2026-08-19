using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WarehouseApi.Middleware;

/// <summary>
///     Central handler for any exception that escapes an endpoint. This replaces the
///     exception-handling-middleware-with-a-lambda pattern from earlier ASP.NET Core versions:
///     IExceptionHandler (introduced in .NET 8) is the idiomatic approach, and as of .NET 10
///     it's wired into the same IProblemDetailsService pipeline that Minimal API validation
///     failures use — so every error the client sees, validation or unhandled exception, has
///     the same application/problem+json shape. Stack traces and exception details are never
///     sent to the client outside Development (OWASP A05 / A09).
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception while processing {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = environment.IsDevelopment() ? exception.ToString() : null,
                Instance = httpContext.Request.Path
            }
        });
    }
}