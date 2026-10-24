using StockManager.Models.DTOs.Common;

namespace StockManager.Models.DTOs;

public record SupplierRequest
{
    public string Name { get; init; } = string.Empty;
    public string? ContactEmail { get; init; }
    public string? Phone { get; init; }
    public string? Address { get; init; }
}

public record SupplierResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

public class SupplierFilterParameters : FilterParameters;
