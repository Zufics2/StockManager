using System.Text.Json.Serialization;

namespace StockManager.Models.DTOs.Common;

/// <summary>Common paging / sorting / free-text search parameters bound from the query string.</summary>
public class FilterParameters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Free-text search (meaning depends on the endpoint).</summary>
    public string? Search { get; set; }

    /// <summary>Field to sort by. Allowed values are listed in the endpoint documentation / validation message.</summary>
    public string? SortBy { get; set; }

    /// <summary>"asc" or "desc". When omitted each endpoint uses its own default.</summary>
    public string? SortDirection { get; set; }

    public bool IsDescending(bool defaultValue) =>
        string.IsNullOrWhiteSpace(SortDirection)
            ? defaultValue
            : string.Equals(SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
}
