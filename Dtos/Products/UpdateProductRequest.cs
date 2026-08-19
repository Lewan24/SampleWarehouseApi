namespace WarehouseApi.Dtos.Products;

public record UpdateProductRequest(string Name, string Category, int Quantity, decimal Price);
