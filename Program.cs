using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using WarehouseApi.Common;
using WarehouseApi.Data;
using WarehouseApi.Endpoints;
using WarehouseApi.Middleware;
using WarehouseApi.Models;
using WarehouseApi.OpenApi;
using WarehouseApi.Services;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, _, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    // Cap request body size to blunt large-payload DoS attempts.
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.Limits.MaxRequestBodySize = 1_000_000; // ~1 MB — adjust per your payloads
    });

    // ---------------------------------------------------------------------
    // Data & Identity
    // ---------------------------------------------------------------------
    var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=warehouse.db";
    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

    builder.Services.AddIdentityCore<ApplicationUser>(options =>
        {
            // Password policy (OWASP ASVS-aligned: length over complexity gymnastics, but we do both here for the demo)
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;

            // Account lockout after repeated failed attempts — mitigates credential stuffing / brute force.
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

    // ---------------------------------------------------------------------
    // Authentication (JWT bearer)
    // ---------------------------------------------------------------------
    var jwtSection = builder.Configuration.GetSection("Jwt");
    var jwtKey = jwtSection["Key"];

    if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
        throw new InvalidOperationException(
            "Jwt:Key must be configured and at least 32 characters (256 bits) long. " +
            "Set it via 'dotnet user-secrets set Jwt:Key \"...\"' locally, or the Jwt__Key " +
            "environment variable in other environments — never commit it to source control.");

    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            options.SaveToken = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSection["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSection["Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };
        });

    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(Policies.AdminOnly, p => p.RequireRole(Roles.Admin))
        .AddPolicy(Policies.ManagerOrAdmin, p => p.RequireRole(Roles.Manager, Roles.Admin))
        .AddPolicy(Policies.ViewerOrAbove, p => p.RequireRole(Roles.Viewer, Roles.Manager, Roles.Admin));

    // ---------------------------------------------------------------------
    // Rate limiting (OWASP API4: Unrestricted Resource Consumption)
    // ---------------------------------------------------------------------
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.Headers.RetryAfter = "60";
            await context.HttpContext.Response.WriteAsJsonAsync(
                new { error = "Too many requests. Please try again later." }, token);
        };

        // Global limiter applied to every request: partitioned per authenticated user
        // (so one noisy user can't starve others) or per IP for anonymous traffic.
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            var key = httpContext.User.Identity?.IsAuthenticated == true
                ? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous"
                : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
        });

        // Much tighter limit on auth endpoints specifically — these are the
        // highest-value target for brute force / credential stuffing.
        options.AddPolicy("auth", httpContext =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
        });
    });

    // ---------------------------------------------------------------------
    // CORS — deny-by-default; only origins explicitly configured are allowed
    // ---------------------------------------------------------------------
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Default", policy =>
        {
            if (allowedOrigins.Length > 0)
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .WithMethods("GET", "POST", "PUT", "DELETE")
                    .AllowCredentials();
            // If nothing is configured, no origins are allowed — fail closed, not open.
        });
    });

    // ---------------------------------------------------------------------
    // App services
    // ---------------------------------------------------------------------
    builder.Services.AddScoped<ITokenService, TokenService>();

    // .NET 10 built-in Minimal API validation: DataAnnotations on request DTOs (and
    // IValidatableObject for cross-field rules) are now enforced automatically for
    // query/header/body-bound parameters — no FluentValidation or custom endpoint
    // filter required. See the Dtos/ folder.
    builder.Services.AddValidation();

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // Native OpenAPI document generation (OpenAPI 3.1, Microsoft.OpenApi 2.0) —
    // no Swashbuckle dependency. Scalar below provides the browsable UI.
    builder.Services.AddOpenApi(options => { options.AddDocumentTransformer<BearerSecuritySchemeTransformer>(); });

    var app = builder.Build();

    // ---------------------------------------------------------------------
    // Database: create schema + seed roles/admin.
    // A template-friendly shortcut — for a real deployment, replace with
    // EF Core migrations (`dotnet ef migrations add InitialCreate`, then
    // `dotnet ef database update`) so schema changes are tracked and reversible.
    // ---------------------------------------------------------------------
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await DbSeeder.SeedAsync(scope.ServiceProvider, app.Configuration, app.Logger);
    }

    // ---------------------------------------------------------------------
    // Middleware pipeline — order matters
    // ---------------------------------------------------------------------
    app.UseSerilogRequestLogging();

    // GlobalExceptionHandler (IExceptionHandler, registered above) does the actual work;
    // this just wires it into the pipeline. No stack traces reach the client outside Development.
    app.UseExceptionHandler();

    if (!app.Environment.IsDevelopment()) app.UseHsts();

    app.UseHttpsRedirection();
    app.UseSecurityHeaders();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi(); // GET /openapi/v1.json
        app.MapScalarApiReference(); // Browsable UI at /scalar/v1
    }

    app.UseCors("Default");
    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapGet("/health", () => Results.Ok(new { status = "healthy", timeUtc = DateTime.UtcNow }))
        .AllowAnonymous()
        .WithTags("Health");

    app.MapAuthEndpoints();
    app.MapProductEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}