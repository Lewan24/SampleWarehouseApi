using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WarehouseApi.Common;
using WarehouseApi.Data;
using WarehouseApi.Dtos.Auth;
using WarehouseApi.Models;
using WarehouseApi.Services;

namespace WarehouseApi.Endpoints;

public static class AuthEndpoints
{
    /// <summary>
    /// The refresh token cookie is scoped to /api/auth only — the browser won't attach it
    /// to /api/products or anything else, which shrinks the blast radius if some other
    /// endpoint ever had a request-forwarding bug.
    /// </summary>
    private const string RefreshTokenCookieName = "warehouse_refresh_token";
    private const string RefreshTokenCookiePath = "/api/auth";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Auth");

        // Request validation (DataAnnotations on the DTOs) happens automatically via
        // builder.Services.AddValidation() — no endpoint filter needed here anymore.
        group.MapPost("/register", RegisterAsync)
            .RequireRateLimiting("auth-strict");;
        group.MapPost("/login", LoginAsync)
            .RequireRateLimiting("auth-strict");;
        group.MapPost("/refresh", RefreshAsync)
            .RequireRateLimiting("auth-refresh");;
        group.MapPost("/revoke", RevokeAsync)
            .RequireAuthorization()
            .RequireRateLimiting("auth-strict");;
    }

    private static async Task<Results<Created<RegisteredUserResponse>, ValidationProblem, Conflict<ErrorResponse>>> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        ILogger<Program> logger)
    {
        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            // Deliberately generic — confirming "this email is already registered"
            // is a user-enumeration vector (OWASP A07).
            return TypedResults.Conflict(new ErrorResponse("Unable to register with the provided details."));
        }

        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(
                result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
        }

        await userManager.AddToRoleAsync(user, Roles.Viewer);
        logger.LogInformation("New user registered: {UserId}", user.Id);

        return TypedResults.Created($"/api/auth/users/{user.Id}", new RegisteredUserResponse(user.Id, user.Email!));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        AppDbContext db,
        HttpContext http,
        IHostEnvironment environment,
        ILogger<Program> logger)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        var checkResult = user is not null
            ? await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)
            : SignInResult.Failed;

        if (user is null || !checkResult.Succeeded)
        {
            // Same generic response whether the email doesn't exist, the password is
            // wrong, or the account is locked out — avoids leaking account state.
            logger.LogWarning("Failed login attempt for {Email}", request.Email);
            return TypedResults.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, accessTokenExpiresAtUtc) = tokenService.GenerateAccessToken(user, roles);
        var (refreshToken, hash, expiresAtUtc) = tokenService.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByIp = http.Connection.RemoteIpAddress?.ToString()
        });
        await db.SaveChangesAsync();

        SetRefreshTokenCookie(http, refreshToken, expiresAtUtc, environment);

        return TypedResults.Ok(new AuthResponse(accessToken, accessTokenExpiresAtUtc));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult>> RefreshAsync(
        HttpContext http,
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IHostEnvironment environment)
    {
        // CSRF guard: this endpoint authenticates purely via cookie (no bearer token, no
        // body), which a plain cross-site <form> POST could otherwise trigger without any
        // preflight. Requiring this custom header forces the browser into a CORS preflight
        // first, which fails for any origin not in Cors:AllowedOrigins — a bare <form> POST
        // can't add custom headers at all. The frontend sets it on every request by default.
        if (http.Request.Headers["X-Requested-With"] != "warehouse-web")
        {
            return TypedResults.Unauthorized();
        }

        if (!http.Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken) || string.IsNullOrEmpty(refreshToken))
        {
            return TypedResults.Unauthorized();
        }

        var hash = tokenService.HashToken(refreshToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);

        if (existing is null)
        {
            return TypedResults.Unauthorized();
        }

        if (existing.RevokedAtUtc is not null)
        {
            // A previously-revoked (already-used) token was presented again.
            // That's a strong signal of token theft/replay — kill the whole
            // refresh-token family for this user so a stolen token can't be used further.
            var activeTokens = await db.RefreshTokens
                .Where(r => r.UserId == existing.UserId && r.RevokedAtUtc == null)
                .ToListAsync();

            foreach (var token in activeTokens)
            {
                token.RevokedAtUtc = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();

            ClearRefreshTokenCookie(http, environment);
            return TypedResults.Unauthorized();
        }

        if (!existing.IsActive)
        {
            ClearRefreshTokenCookie(http, environment);
            return TypedResults.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(existing.UserId);
        if (user is null)
        {
            ClearRefreshTokenCookie(http, environment);
            return TypedResults.Unauthorized();
        }

        // Rotate: the old token is consumed, a new one takes its place.
        existing.RevokedAtUtc = DateTime.UtcNow;

        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, accessTokenExpiresAtUtc) = tokenService.GenerateAccessToken(user, roles);
        var (newRefreshToken, newHash, newExpiresAtUtc) = tokenService.GenerateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newHash,
            ExpiresAtUtc = newExpiresAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedByIp = http.Connection.RemoteIpAddress?.ToString()
        });
        await db.SaveChangesAsync();

        SetRefreshTokenCookie(http, newRefreshToken, newExpiresAtUtc, environment);

        return TypedResults.Ok(new AuthResponse(accessToken, accessTokenExpiresAtUtc));
    }

    private static async Task<NoContent> RevokeAsync(HttpContext http, AppDbContext db, ITokenService tokenService, IHostEnvironment environment)
    {
        if (http.Request.Cookies.TryGetValue(RefreshTokenCookieName, out var refreshToken) && !string.IsNullOrEmpty(refreshToken))
        {
            var hash = tokenService.HashToken(refreshToken);
            var existing = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);

            if (existing is not null && existing.RevokedAtUtc is null)
            {
                existing.RevokedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        ClearRefreshTokenCookie(http, environment);

        // 204 either way — don't reveal whether the token existed.
        return TypedResults.NoContent();
    }

    /// <summary>
    /// In Development, the frontend talks to the API through Vite's dev proxy (see
    /// warehouse-web/vite.config.ts), so browser and API are same-origin and a plain
    /// Lax cookie over HTTP works fine — no dev TLS setup required.
    /// In any other environment, this assumes a genuinely cross-origin deployment
    /// (separate frontend/API domains), which requires SameSite=None + Secure. If you
    /// instead deploy both behind one reverse-proxy domain, switch this to Lax — it's
    /// strictly safer against CSRF than None.
    /// </summary>
    private static void SetRefreshTokenCookie(HttpContext http, string token, DateTime expiresAtUtc, IHostEnvironment environment)
    {
        http.Response.Cookies.Append(RefreshTokenCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None,
            Path = RefreshTokenCookiePath,
            Expires = expiresAtUtc
        });
    }

    private static void ClearRefreshTokenCookie(HttpContext http, IHostEnvironment environment)
    {
        http.Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = environment.IsDevelopment() ? SameSiteMode.Lax : SameSiteMode.None,
            Path = RefreshTokenCookiePath
        });
    }
}
