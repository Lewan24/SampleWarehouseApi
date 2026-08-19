using System.ComponentModel.DataAnnotations;

namespace WarehouseApi.Dtos.Auth;

public record RefreshRequest([property: Required] string RefreshToken);