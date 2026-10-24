namespace StockManager.Models.Entities;

public class StockMovement : IAuditable
{
    public long Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public MovementType Type { get; set; }

    /// <summary>Always positive; direction is given by <see cref="Type"/>.</summary>
    public int Quantity { get; set; }

    /// <summary>Warehouse balance of the product right after this movement.</summary>
    public int QuantityAfter { get; set; }

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public decimal? UnitCost { get; set; }

    /// <summary>Note for receipts, mandatory reason for write-offs.</summary>
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
}
