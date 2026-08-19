using WarehouseApi.Models;

namespace WarehouseApi.Services;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);

    (string Token, string Hash, DateTime ExpiresAtUtc) GenerateRefreshToken();

    string HashToken(string token);
}
