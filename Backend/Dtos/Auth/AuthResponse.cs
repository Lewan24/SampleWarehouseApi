namespace WarehouseApi.Dtos.Auth;

/// <summary>
/// The refresh token is intentionally NOT here. For browser clients it travels as an
/// httpOnly, Secure cookie instead (see AuthEndpoints.RefreshTokenCookieName) so
/// JavaScript — and therefore XSS — can never read it. Only the short-lived access
/// token goes in the JSON body, where the SPA holds it in memory.
/// </summary>
public record AuthResponse(string AccessToken, DateTime ExpiresAtUtc);
