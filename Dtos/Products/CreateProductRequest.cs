namespace WarehouseApi.Dtos.Products;

public record CreateProductRequest(string Name, string Sku, string Category, int Quantity, decimal Price);
