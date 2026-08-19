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
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Auth")
            .RequireRateLimiting("auth"); // Tighter limiter than the rest of the API — see Program.cs

        group.MapPost("/register", RegisterAsync)
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>()
            .Produces(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .Produces<AuthResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshAsync)
            .Produces<AuthResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/revoke", RevokeAsync)
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent);
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager,
        ILogger<Program> logger)
    {
        var existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            // Deliberately generic — confirming "this email is already registered"
            // is a user-enumeration vector (OWASP A07).
            return Results.Conflict(new { error = "Unable to register with the provided details." });
        }

        var user = new ApplicationUser { UserName = request.Email, Email = request.Email };
        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return Results.ValidationProblem(
                result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
        }

        await userManager.AddToRoleAsync(user, Roles.Viewer);
        logger.LogInformation("New user registered: {UserId}", user.Id);

        return Results.Created($"/api/auth/users/{user.Id}", new { user.Id, user.Email });
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        AppDbContext db,
        HttpContext http,
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
            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.GenerateAccessToken(user, roles);
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

        return Results.Ok(new AuthResponse(accessToken, refreshToken, expiresAtUtc));
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        HttpContext http)
    {
        var hash = tokenService.HashToken(request.RefreshToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);

        if (existing is null)
        {
            return Results.Unauthorized();
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

            return Results.Unauthorized();
        }

        if (!existing.IsActive)
        {
            return Results.Unauthorized();
        }

        var user = await userManager.FindByIdAsync(existing.UserId);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        // Rotate: the old token is consumed, a new one takes its place.
        existing.RevokedAtUtc = DateTime.UtcNow;

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.GenerateAccessToken(user, roles);
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

        return Results.Ok(new AuthResponse(accessToken, newRefreshToken, newExpiresAtUtc));
    }

    private static async Task<IResult> RevokeAsync(RefreshRequest request, AppDbContext db, ITokenService tokenService)
    {
        var hash = tokenService.HashToken(request.RefreshToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash);

        if (existing is not null && existing.RevokedAtUtc is null)
        {
            existing.RevokedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        // 204 either way — don't reveal whether the token existed.
        return Results.NoContent();
    }
}
