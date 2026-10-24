using FluentValidation;
using StockManager.Models.DTOs;
using StockManager.Service.Services;

namespace StockManager.Service.Validators;

public class ProductRequestValidator : AbstractValidator<ProductRequest>
{
    public ProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200).WithMessage("Product name must not exceed 200 characters.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("SKU is required.")
            .MaximumLength(50).WithMessage("SKU must not exceed 50 characters.")
            .Matches(@"^[A-Za-z0-9._\-]+$")
            .WithMessage("SKU may contain only letters, digits, '.', '_' and '-'.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero.")
            .LessThanOrEqualTo(1_000_000_000m).WithMessage("Price must not exceed 1,000,000,000.")
            .PrecisionScale(18, 2, false).WithMessage("Price can have at most 2 decimal places.");

        RuleFor(x => x.CategoryId)
            .GreaterThan(0).WithMessage("A valid categoryId is required.");

        RuleFor(x => x.SupplierId)
            .GreaterThan(0).WithMessage("SupplierId must be a positive number.")
            .When(x => x.SupplierId.HasValue);
    }
}

public class ProductFilterValidator : BaseFilterValidator<ProductFilterParameters>
{
    public ProductFilterValidator() : base(ProductService.SortFields.Keys)
    {
        RuleFor(x => x.MinPrice)
            .GreaterThanOrEqualTo(0).WithMessage("MinPrice must not be negative.")
            .When(x => x.MinPrice.HasValue);

        RuleFor(x => x.MaxPrice)
            .GreaterThanOrEqualTo(0).WithMessage("MaxPrice must not be negative.")
            .When(x => x.MaxPrice.HasValue);

        RuleFor(x => x)
            .Must(x => x.MinPrice <= x.MaxPrice)
            .When(x => x.MinPrice.HasValue && x.MaxPrice.HasValue)
            .OverridePropertyName("MinPrice")
            .WithMessage("MinPrice must not be greater than MaxPrice.");
    }
}

public class PriceConversionValidator : AbstractValidator<PriceConversionParameters>
{
    public PriceConversionValidator()
    {
        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required (e.g. EUR).")
            .Matches("^[A-Za-z]{3}$").WithMessage("Currency must be a 3-letter ISO 4217 code (e.g. EUR, KZT).");
    }
}
