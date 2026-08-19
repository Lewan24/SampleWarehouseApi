namespace WarehouseApi.Dtos.Products;

public record ProductDto(
    Guid Id,
    string Name,
    string Sku,
    string Category,
    int Quantity,
    decimal Price,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
