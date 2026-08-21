using System.ComponentModel.DataAnnotations;

namespace WarehouseApi.Dtos.Products;

public record CreateProductRequest(
    [property: Required, MaxLength(150)]
    string Name,

    [property: Required, MaxLength(50)]
    [property: RegularExpression("^[A-Za-z0-9-_]+$", ErrorMessage = "SKU may only contain letters, numbers, hyphens and underscores.")]
    string Sku,

    [property: Required, MaxLength(80)]
    string Category,

    [property: Range(0, int.MaxValue)]
    int Quantity,

    [property: Range(typeof(decimal), "0", "999999")]
    decimal Price);
