using WarehouseApi.Models;

namespace WarehouseApi.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);

    (string Token, string Hash, DateTime ExpiresAtUtc) GenerateRefreshToken();

    string HashToken(string token);
}
