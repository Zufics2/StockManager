using StockManager.Models.DTOs.Common;
using StockManager.Models.Entities;

namespace StockManager.Models.DTOs;

/// <summary>Goods arrive at a warehouse.</summary>
public record StockReceiptRequest
{
    public int ProductId { get; init; }
    public int WarehouseId { get; init; }
    public int Quantity { get; init; }
    public int? SupplierId { get; init; }
    public decimal? UnitCost { get; init; }
    public string? Note { get; init; }
}

/// <summary>Goods leave a warehouse (write-off / disposal). A reason is mandatory.</summary>
public record StockWriteOffRequest
{
    public int ProductId { get; init; }
    public int WarehouseId { get; init; }
    public int Quantity { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>Internal, direction-agnostic model that is sent through MediatR.</summary>
public record RegisterStockMovementRequest
{
    public MovementType Type { get; init; }
    public int ProductId { get; init; }
    public int WarehouseId { get; init; }
    public int Quantity { get; init; }
    public int? SupplierId { get; init; }
    public decimal? UnitCost { get; init; }
    public string? Reason { get; init; }
}

public record StockMovementResponse
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public MovementType Type { get; set; }
    public int Quantity { get; set; }
    public int QuantityAfter { get; set; }
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class StockMovementFilterParameters : FilterParameters
{
    public int? ProductId { get; set; }
    public int? WarehouseId { get; set; }
    public MovementType? Type { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
