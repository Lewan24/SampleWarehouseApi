using System.ComponentModel.DataAnnotations;

namespace WarehouseApi.Dtos.Auth;

public record LoginRequest(
    [property: Required]
    [property: EmailAddress]
    string Email,
    [property: Required] string Password);