namespace WarehouseApi.Dtos.Auth;

public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
