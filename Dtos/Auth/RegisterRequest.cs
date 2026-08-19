namespace WarehouseApi.Dtos.Auth;

public record RegisterRequest(string Email, string Password, string ConfirmPassword);
