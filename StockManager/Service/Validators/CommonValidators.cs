using FluentValidation;
using StockManager.Models.DTOs.Common;

namespace StockManager.Service.Validators;

/// <summary>Shared rules for paging, sorting and free-text search.</summary>
public abstract class BaseFilterValidator<T> : AbstractValidator<T> where T : FilterParameters
{
    protected BaseFilterValidator(IEnumerable<string> allowedSortFields)
    {
        var allowed = allowedSortFields.ToArray();

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or greater.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");

        RuleFor(x => x.Search)
            .MaximumLength(100).WithMessage("Search text must not exceed 100 characters.");

        RuleFor(x => x.SortDirection)
            .Must(d => string.IsNullOrWhiteSpace(d)
                       || d.Equals("asc", StringComparison.OrdinalIgnoreCase)
                       || d.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortDirection must be either 'asc' or 'desc'.");

        RuleFor(x => x.SortBy)
            .Must(s => string.IsNullOrWhiteSpace(s) || allowed.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"SortBy must be one of: {string.Join(", ", allowed)}.");
    }
}
