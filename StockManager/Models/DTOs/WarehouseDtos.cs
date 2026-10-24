using StockManager.Models.DTOs.Common;

namespace StockManager.Models.DTOs;

public record WarehouseRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Location { get; init; }
}

public record WarehouseResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
}

public record StockLevelResponse
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WarehouseFilterParameters : FilterParameters;

public class StockLevelFilterParameters : FilterParameters;
