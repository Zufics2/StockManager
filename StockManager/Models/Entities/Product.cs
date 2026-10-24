namespace StockManager.Models.Entities;

public class Product : IAuditable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Stock keeping unit. Stored upper-case and unique.</summary>
    public string Sku { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Unit price in the configured base currency.</summary>
    public decimal Price { get; set; }

    /// <summary>Relative URL of the uploaded image (e.g. /uploads/products/abc.png).</summary>
    public string? ImagePath { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<StockLevel> StockLevels { get; set; } = new List<StockLevel>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
