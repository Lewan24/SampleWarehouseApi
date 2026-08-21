# Warehouse API

A small, security-focused .NET 10 minimal API template. It models a simple warehouse
(products in, products out) but the point of the project is the scaffolding around
it: JWT auth with refresh-token rotation, role-based authorization, rate limiting,
and a handful of OWASP Top 10 mitigations, all in a codebase small enough to read
in one sitting.

## Stack (.NET 10)

- **.NET 10** minimal APIs (no MVC controllers), C# 14
- **ASP.NET Core Identity** for user/password/lockout management
- **JWT bearer** access tokens + a custom, hashed, rotating refresh-token store
- **EF Core 10 + SQLite** (zero external dependencies to run locally)
- **Built-in Minimal API validation** (`builder.Services.AddValidation()`) — DataAnnotations
  on the request DTOs, no FluentValidation dependency
- **`IExceptionHandler`** wired into `IProblemDetailsService`, so unhandled exceptions and
  validation failures return the same `application/problem+json` shape
- **`TypedResults` / `Results<T1,T2,...>`** on every endpoint — return types are part of the
  method signature, so the compiler catches a missing branch and OpenAPI metadata is
  generated for free, no `.Produces<T>()` calls needed
- **Native OpenAPI** (`Microsoft.AspNetCore.OpenApi`, OpenAPI 3.1) + **Scalar** for the
  browsable API reference UI — no Swashbuckle
- **Serilog** structured logging
- Built-in **System.Threading.RateLimiting** middleware (no extra package needed)

## Project layout

```
Program.cs              composition root: DI, security pipeline, endpoint mapping
Data/                    AppDbContext, role/admin seeding
Models/                  EF entities (ApplicationUser, RefreshToken, Product)
Dtos/                    request/response contracts, validated via DataAnnotations
Services/                TokenService (JWT + refresh token issuance/hashing)
Endpoints/               AuthEndpoints, ProductEndpoints (TypedResults, grouped routes)
Middleware/              GlobalExceptionHandler, security response headers
OpenApi/                 BearerSecuritySchemeTransformer (adds JWT auth to the OpenAPI doc)
Common/                  roles/policies constants, PagedResult, ErrorResponse
```

## Running it

Requires the **.NET 10 SDK**.

```bash
cd WarehouseApi
dotnet restore

# Set the JWT signing key and seed-admin credentials as user secrets
# (never commit real secrets to appsettings.json)
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "a-random-string-at-least-32-characters-long"
dotnet user-secrets set "Seed:AdminEmail" "admin@example.com"
dotnet user-secrets set "Seed:AdminPassword" "SomeStrongPassword123!"

dotnet run
```

The Scalar API reference opens at `/scalar/v1` in Development (backed by the raw OpenAPI
document at `/openapi/v1.json`). `WarehouseApi.http` has ready-to-run sample requests if
you use VS Code's REST Client or Visual Studio's built-in HTTP editor.

The SQLite database (`warehouse.db`) and its schema are created automatically on
first run via `EnsureCreatedAsync`. That's a template-friendly shortcut — for a real
project, switch to EF Core migrations so schema changes are tracked and reversible:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

> **A note on package versions:** they're pinned to the initial .NET 10 GA line
> (`10.0.0`). Run `dotnet restore` and bump to the latest matching patch version if
> one isn't found — I wrote this offline and couldn't verify against a live NuGet feed.

## What changed from the .NET 8 version of this template

| Then (.NET 8) | Now (.NET 10) |
|---|---|
| FluentValidation + a custom `ValidationFilter<T>` endpoint filter | Built-in Minimal API validation (`AddValidation()`) — DataAnnotations on DTOs, `IValidatableObject` for cross-field rules like password confirmation |
| Swashbuckle (Swagger UI, OpenAPI 2.0/3.0) | Native `Microsoft.AspNetCore.OpenApi` (OpenAPI 3.1) + Scalar for the UI |
| `app.UseExceptionHandler(errorApp => errorApp.Run(...))` lambda middleware | `GlobalExceptionHandler : IExceptionHandler`, registered via DI and integrated with `IProblemDetailsService` |
| Endpoints returning `IResult` / `Results.Ok(...)` | Endpoints returning `TypedResults` / `Results<Ok<T>, NotFound, ...>` union types — compiler-checked, self-documenting for OpenAPI |
| `WarningsAsErrors` nullable setting removed for build safety | unchanged — still relaxed, since I can't compile-verify this in the environment I built it in |

Everything else — the auth flow, refresh-token rotation and reuse detection, rate
limiting, security headers, role policies — is conceptually the same; only the
plumbing around it got more modern.

## Roles & policies

Three roles: `Viewer`, `Manager`, `Admin`. New self-registered users get `Viewer`.
The seeded bootstrap account gets `Admin`; use it to build out further users/roles
as needed (a `POST /api/auth/assign-role`-style admin endpoint is a natural next
addition once you need it).

| Endpoint | Policy |
|---|---|
| `GET /api/products`, `GET /api/products/{id}` | `ViewerOrAbove` |
| `POST /api/products`, `PUT /api/products/{id}` | `ManagerOrAdmin` |
| `DELETE /api/products/{id}` | `AdminOnly` |
| `POST /api/auth/*` | anonymous, rate-limited |

## Security practices implemented, mapped to OWASP Top 10 (2021)

| Risk | What this template does |
|---|---|
| **A01 Broken Access Control** | Every write/delete endpoint requires an explicit authorization policy (no "authenticated = allowed everything"); roles are checked server-side from JWT claims, never trusted from client input. |
| **A02 Cryptographic Failures** | Passwords hashed by ASP.NET Core Identity (PBKDF2-HMACSHA256); refresh tokens stored as SHA-256 hashes, never in plaintext; JWT signing key loaded from user-secrets/environment, never checked into source; HTTPS enforced + HSTS in non-dev. |
| **A03 Injection** | All data access goes through EF Core LINQ (parameterized queries); the one text-search filter uses `EF.Functions.Like`, also parameterized. No raw SQL anywhere. |
| **A04 Insecure Design** | Refresh-token rotation with reuse detection (a replayed/stolen token revokes the whole token family); account lockout after failed logins; least-privilege roles designed in from the start. |
| **A05 Security Misconfiguration** | `GlobalExceptionHandler` strips stack traces outside Development; security headers middleware (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, locked-down CSP); CORS is deny-by-default; Scalar/OpenAPI only mapped in Development. |
| **A06 Vulnerable/Outdated Components** | Package versions pinned in the `.csproj`. Run `dotnet list package --vulnerable` periodically, or wire up Dependabot/Renovate. |
| **A07 Identification & Authentication Failures** | Short-lived (15 min) access tokens; rotating refresh tokens; strong password policy; account lockout; login/register responses are deliberately generic to avoid user enumeration. |
| **A08 Software & Data Integrity Failures** | JWT signature strictly validated (issuer, audience, lifetime, signing key); no untrusted deserialization anywhere in the request pipeline. |
| **A09 Security Logging & Monitoring Failures** | Structured logging via Serilog for requests, failed logins, and unhandled exceptions — deliberately never logging passwords or tokens. |
| **A10 SSRF** | The API makes no outbound requests based on user input, so there's no surface for this today — worth revisiting the moment you add any "fetch this URL" feature. |

A few extras beyond the Top 10 list: request body size is capped at Kestrel level to
blunt large-payload abuse, and pagination `pageSize` is clamped server-side (an
API-specific flavor of resource-consumption abuse).

## Honest limitations / good next steps

This is a teaching-sized template, not a hardened production system as-is. Before
shipping something like this for real, consider:

- **Distributed rate limiting** — the current limiter is in-memory per instance; behind
  multiple replicas you'd want a shared store (e.g. Redis-backed).
- **RS256 / asymmetric JWT signing** if multiple services need to *validate* tokens
  without being trusted to *issue* them.
- **A real secrets manager** (Azure Key Vault, AWS Secrets Manager, etc.) instead of
  user-secrets/environment variables once you're past local dev.
- **Migrations instead of `EnsureCreatedAsync`** for any real deployment.
- **Integration tests** around the auth flow and policy enforcement — the kind of
  code most likely to silently break during refactors.
- **API versioning** once you have external consumers you can't break.
- **Centralized audit logging** (who changed what, when) if this needs to be audit-able.

## Pairing with a browser frontend

The refresh token is delivered as an httpOnly, `/api/auth`-scoped cookie rather than in
the JSON response body — see the comments in `Endpoints/AuthEndpoints.cs` for why
(short version: JavaScript, and therefore XSS, can never read it). A matching React +
Vite frontend that consumes this API — with the full login/silent-refresh/proactive-
refresh/role-gated-UI flow — lives alongside this project in `warehouse-web/`; its
README explains the auth flow from the browser side in detail.
