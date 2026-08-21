using System.ComponentModel.DataAnnotations;

namespace WarehouseApi.Dtos.Auth;

public record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);
