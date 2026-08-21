namespace WarehouseApi.Models;

/// <summary>
/// Server-side record of an issued refresh token.
///
/// Security notes:
/// - Only a SHA-256 hash of the token is stored (TokenHash), never the raw value —
///   the same principle as password hashing: a DB leak shouldn't hand out usable tokens.
/// - Tokens are single-use and rotated on every refresh (RevokedAtUtc set on use).
/// - If a revoked token is presented again, that's a signal of possible theft/replay,
///   so the whole token family for that user gets revoked (see AuthEndpoints.RefreshAsync).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public required string UserId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? CreatedByIp { get; set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}
