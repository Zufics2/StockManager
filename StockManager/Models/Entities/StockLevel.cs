namespace StockManager.Models.Entities;

/// <summary>
/// Current balance of one product in one warehouse (composite key).
/// Kept in sync by <c>StockMovementService</c>; protected from lost updates by <see cref="ConcurrencyStamp"/>.
/// </summary>
public class StockLevel
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public int WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;

    public int Quantity { get; set; }

    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
