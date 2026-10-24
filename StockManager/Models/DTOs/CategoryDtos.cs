using StockManager.Models.DTOs.Common;

namespace StockManager.Models.DTOs;

public record CategoryRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public record CategoryResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ProductCount { get; set; }
}

public class CategoryFilterParameters : FilterParameters;
