using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using WarehouseApi.Common;
using WarehouseApi.Data;
using WarehouseApi.Dtos.Products;
using WarehouseApi.Mapping;
using WarehouseApi.Models;

namespace WarehouseApi.Endpoints;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        // Request validation (DataAnnotations on CreateProductRequest / UpdateProductRequest)
        // is applied automatically via builder.Services.AddValidation() in Program.cs.
        group.MapGet("/", GetProductsAsync).RequireAuthorization(Policies.ViewerOrAbove);
        group.MapGet("/{id:guid}", GetProductByIdAsync).RequireAuthorization(Policies.ViewerOrAbove);
        group.MapPost("/", CreateProductAsync).RequireAuthorization(Policies.ManagerOrAdmin);
        group.MapPut("/{id:guid}", UpdateProductAsync).RequireAuthorization(Policies.ManagerOrAdmin);
        group.MapDelete("/{id:guid}", DeleteProductAsync).RequireAuthorization(Policies.AdminOnly);
    }

    private static async Task<Ok<PagedResult<ProductDto>>> GetProductsAsync(AppDbContext db, int page = 1,
        int pageSize = 20, string? search = null)
    {
        // Clamp instead of trusting client input directly — an unbounded pageSize
        // is a cheap resource-exhaustion vector (OWASP API4: Unrestricted Resource Consumption).
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            // EF.Functions.Like is translated to a parameterized SQL LIKE — safe from injection.
            query = query.Where(p => EF.Functions.Like(p.Name, $"%{term}%") || EF.Functions.Like(p.Sku, $"%{term}%"));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => p.ToDto())
            .ToListAsync();

        return TypedResults.Ok(new PagedResult<ProductDto>(items, total, page, pageSize));
    }

    private static async Task<Results<Ok<ProductDto>, NotFound>> GetProductByIdAsync(Guid id, AppDbContext db)
    {
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return product is null ? TypedResults.NotFound() : TypedResults.Ok(product.ToDto());
    }

    private static async Task<Results<Created<ProductDto>, Conflict<ErrorResponse>>> CreateProductAsync(
        CreateProductRequest request, AppDbContext db)
    {
        var sku = request.Sku.Trim().ToUpperInvariant();

        var skuExists = await db.Products.AnyAsync(p => p.Sku == sku);
        if (skuExists) return TypedResults.Conflict(new ErrorResponse("A product with this SKU already exists."));

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Sku = sku,
            Category = request.Category.Trim(),
            Quantity = request.Quantity,
            Price = request.Price,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/products/{product.Id}", product.ToDto());
    }

    private static async Task<Results<Ok<ProductDto>, NotFound>> UpdateProductAsync(Guid id,
        UpdateProductRequest request, AppDbContext db)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (product is null) return TypedResults.NotFound();

        product.Name = request.Name.Trim();
        product.Category = request.Category.Trim();
        product.Quantity = request.Quantity;
        product.Price = request.Price;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return TypedResults.Ok(product.ToDto());
    }

    private static async Task<Results<NoContent, NotFound>> DeleteProductAsync(Guid id, AppDbContext db)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return TypedResults.NotFound();

        db.Products.Remove(product);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }
}