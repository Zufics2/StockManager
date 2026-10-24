namespace StockManager.Models.Entities;

public class Supplier : IAuditable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}
