using FluentValidation;
using StockManager.Models.DTOs;
using StockManager.Service.Services;

namespace StockManager.Service.Validators;

public class CategoryRequestValidator : AbstractValidator<CategoryRequest>
{
    public CategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MaximumLength(100).WithMessage("Category name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
    }
}

public class SupplierRequestValidator : AbstractValidator<SupplierRequest>
{
    public SupplierRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Supplier name is required.")
            .MaximumLength(150).WithMessage("Supplier name must not exceed 150 characters.");

        RuleFor(x => x.ContactEmail)
            .EmailAddress().WithMessage("Contact email is not a valid email address.")
            .MaximumLength(256).WithMessage("Contact email must not exceed 256 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));

        RuleFor(x => x.Phone)
            .Matches(@"^\+?[0-9 ()\-]{5,30}$")
            .WithMessage("Phone may contain digits, spaces, '+', '-', '(' and ')' and must be 5-30 characters long.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Address)
            .MaximumLength(300).WithMessage("Address must not exceed 300 characters.");
    }
}

public class WarehouseRequestValidator : AbstractValidator<WarehouseRequest>
{
    public WarehouseRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Warehouse name is required.")
            .MaximumLength(100).WithMessage("Warehouse name must not exceed 100 characters.");

        RuleFor(x => x.Location)
            .MaximumLength(300).WithMessage("Location must not exceed 300 characters.");
    }
}

public class CategoryFilterValidator() : BaseFilterValidator<CategoryFilterParameters>(CategoryService.SortFields.Keys);

public class SupplierFilterValidator() : BaseFilterValidator<SupplierFilterParameters>(SupplierService.SortFields.Keys);

public class WarehouseFilterValidator() : BaseFilterValidator<WarehouseFilterParameters>(WarehouseService.SortFields.Keys);

public class StockLevelFilterValidator() : BaseFilterValidator<StockLevelFilterParameters>(WarehouseService.StockSortFields.Keys);

public class AuditLogFilterValidator : BaseFilterValidator<AuditLogFilterParameters>
{
    public AuditLogFilterValidator() : base(AuditLogService.SortFields.Keys)
    {
        RuleFor(x => x.To)
            .Must((x, to) => to >= x.From)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("'To' must not be earlier than 'From'.");
    }
}
