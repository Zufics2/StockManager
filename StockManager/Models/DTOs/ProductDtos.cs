using StockManager.Models.DTOs.Common;

namespace StockManager.Models.DTOs;

/// <summary>Request body for both creating and updating a product.</summary>
public record ProductRequest
{
    public string Name { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public int CategoryId { get; init; }
    public int? SupplierId { get; init; }
}

public record ProductResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>Sum of the product's balances over all warehouses.</summary>
    public int TotalStock { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class ProductFilterParameters : FilterParameters
{
    public int? CategoryId { get; set; }
    public int? SupplierId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    /// <summary>true = only products with stock &gt; 0, false = only products without stock.</summary>
    public bool? InStock { get; set; }
}

public record ProductImageResponse
{
    public string ImageUrl { get; set; } = string.Empty;
}

public class PriceConversionParameters
{
    /// <summary>ISO 4217 code, e.g. EUR, KZT.</summary>
    public string Currency { get; set; } = string.Empty;
}

public record ProductPriceResponse
{
    public int ProductId { get; set; }
    public string BaseCurrency { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public string TargetCurrency { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public decimal ConvertedPrice { get; set; }
    public DateTime RateRetrievedAtUtc { get; set; }
}

/// <summary>Result of the external exchange-rate lookup.</summary>
public record ExchangeRateResult(string BaseCurrency, string TargetCurrency, decimal Rate, DateTime RetrievedAtUtc);
