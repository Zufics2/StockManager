using FluentValidation;
using StockManager.Models.DTOs;
using StockManager.Service.Services;

namespace StockManager.Service.Validators;

public class StockReceiptRequestValidator : AbstractValidator<StockReceiptRequest>
{
    public StockReceiptRequestValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("A valid productId is required.");
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("A valid warehouseId is required.");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 1_000_000).WithMessage("Quantity must be between 1 and 1,000,000.");

        RuleFor(x => x.SupplierId)
            .GreaterThan(0).WithMessage("SupplierId must be a positive number.")
            .When(x => x.SupplierId.HasValue);

        RuleFor(x => x.UnitCost)
            .GreaterThanOrEqualTo(0).WithMessage("Unit cost must not be negative.")
            .PrecisionScale(18, 2, false).WithMessage("Unit cost can have at most 2 decimal places.")
            .When(x => x.UnitCost.HasValue);

        RuleFor(x => x.Note)
            .MaximumLength(500).WithMessage("Note must not exceed 500 characters.");
    }
}

public class StockWriteOffRequestValidator : AbstractValidator<StockWriteOffRequest>
{
    public StockWriteOffRequestValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("A valid productId is required.");
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("A valid warehouseId is required.");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 1_000_000).WithMessage("Quantity must be between 1 and 1,000,000.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reason is required for a write-off.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}

public class StockMovementFilterValidator : BaseFilterValidator<StockMovementFilterParameters>
{
    public StockMovementFilterValidator() : base(StockMovementService.SortFields.Keys)
    {
        RuleFor(x => x.To)
            .Must((x, to) => to >= x.From)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("'To' must not be earlier than 'From'.");
    }
}
