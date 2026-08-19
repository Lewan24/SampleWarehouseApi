using FluentValidation;
using WarehouseApi.Dtos.Products;

namespace WarehouseApi.Validators;

public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);

        RuleFor(x => x.Sku)
            .NotEmpty()
            .MaximumLength(50)
            .Matches("^[A-Za-z0-9-_]+$")
            .WithMessage("SKU may only contain letters, numbers, hyphens and underscores.");

        RuleFor(x => x.Category).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).LessThan(1_000_000);
    }
}
