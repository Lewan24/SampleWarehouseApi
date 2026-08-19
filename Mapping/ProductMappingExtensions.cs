using WarehouseApi.Dtos.Products;
using WarehouseApi.Models;

namespace WarehouseApi.Mapping;

public static class ProductMappingExtensions
{
    public static ProductDto ToDto(this Product product) => new(
        product.Id,
        product.Name,
        product.Sku,
        product.Category,
        product.Quantity,
        product.Price,
        product.CreatedAtUtc,
        product.UpdatedAtUtc);
}
