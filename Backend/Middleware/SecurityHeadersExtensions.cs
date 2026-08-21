namespace WarehouseApi.Middleware;

/// <summary>
/// Adds a baseline set of hardening headers to every response, aligned with the
/// OWASP Secure Headers Project. HSTS is configured separately via app.UseHsts().
/// </summary>
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            // This is a JSON API with no HTML surface, so a locked-down CSP is safe.
            // Scalar's API reference UI (dev only) needs its own scripts/styles, so it's excluded.
            var path = context.Request.Path;
            if (!path.StartsWithSegments("/scalar") && !path.StartsWithSegments("/openapi"))
            {
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            }

            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            await next();
        });
    }
}
