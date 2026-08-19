using Microsoft.AspNetCore.Identity;

namespace WarehouseApi.Models;

/// <summary>
/// Extends IdentityUser so we can add app-specific fields later without a breaking change.
/// Password hashing, security stamps, and lockout bookkeeping are all handled by
/// ASP.NET Core Identity (PBKDF2-HMACSHA256 by default) — we never touch raw passwords.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
