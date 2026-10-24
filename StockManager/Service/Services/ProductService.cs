using System.Linq.Expressions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StockManager.Abstract;
using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;
using StockManager.Models.Entities;
using StockManager.Models.Exceptions;
using StockManager.Models.Options;
using StockManager.Service.Data;
using StockManager.Service.Extensions;

namespace StockManager.Service.Services;

public class ProductService(
    ApplicationDbContext db,
    IMapper mapper,
    IFileService files,
    ICurrencyService currency,
    IOptions<CurrencyOptions> currencyOptions) : IProductService
{
    public static readonly IReadOnlyDictionary<string, Expression<Func<Product, object>>> SortFields =
        new Dictionary<string, Expression<Func<Product, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = p => p.Name,
            ["sku"] = p => p.Sku,
            ["price"] = p => p.Price,
            ["createdAt"] = p => p.CreatedAt
        };

    public async Task<PagedResult<ProductResponse>> GetPagedAsync(ProductFilterParameters filter, CancellationToken ct = default)
    {
        IQueryable<Product> query = db.Products.AsNoTracking();
        
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term)
                                     || p.Sku.ToLower().Contains(term)
                                     || (p.Description != null && p.Description.ToLower().Contains(term)));
        }
        
        if (filter.CategoryId is { } categoryId)
            query = query.Where(p => p.CategoryId == categoryId);

        if (filter.SupplierId is { } supplierId)
            query = query.Where(p => p.SupplierId == supplierId);
        
        if (filter.MinPrice is { } min)
            query = query.Where(p => p.Price >= min);

        if (filter.MaxPrice is { } max)
            query = query.Where(p => p.Price <= max);

        if (filter.InStock is { } inStock)
            query = inStock
                ? query.Where(p => p.StockLevels.Any(l => l.Quantity > 0))
                : query.Where(p => !p.StockLevels.Any(l => l.Quantity > 0));

        query = query.ApplySorting(filter.SortBy, filter.IsDescending(false), SortFields, "name", p => p.Id);

        return await query.ToPagedResultAsync<Product, ProductResponse>(mapper.ConfigurationProvider, filter.Page, filter.PageSize, ct);
    }

    public async Task<ProductResponse> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .ProjectTo<ProductResponse>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(Product), id);

    public async Task<ProductResponse> CreateAsync(ProductRequest request, CancellationToken ct = default)
    {
        var sku = NormalizeSku(request.Sku);
        await EnsureSkuIsFreeAsync(sku, null, ct);
        await EnsureReferencesExistAsync(request, ct);

        var product = mapper.Map<Product>(request);
        product.CreatedAt = DateTime.UtcNow;

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(product.Id, ct);
    }

    public async Task<ProductResponse> UpdateAsync(int id, ProductRequest request, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException(nameof(Product), id);

        await EnsureSkuIsFreeAsync(NormalizeSku(request.Sku), id, ct);
        await EnsureReferencesExistAsync(request, ct);

        mapper.Map(request, product);
        product.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException(nameof(Product), id);

        if (await db.StockMovements.AnyAsync(m => m.ProductId == id, ct))
            throw new ConflictException(
                $"Product '{product.Name}' has stock movements and cannot be deleted (the history must be preserved).");

        var imagePath = product.ImagePath;

        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);

        files.Delete(imagePath);
    }

    public async Task<ProductImageResponse> SetImageAsync(int id, IFormFile file, CancellationToken ct = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException(nameof(Product), id);

        var newPath = await files.SaveImageAsync(file, ct);
        var oldPath = product.ImagePath;

        product.ImagePath = newPath;
        product.UpdatedAt = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            files.Delete(newPath);
            throw;
        }

        files.Delete(oldPath);
        return new ProductImageResponse { ImageUrl = newPath };
    }

    public async Task<ProductPriceResponse> GetConvertedPriceAsync(int id, string targetCurrency, CancellationToken ct = default)
    {
        var price = await db.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => (decimal?)p.Price)
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException(nameof(Product), id);

        var baseCurrency = currencyOptions.Value.BaseCurrency.ToUpperInvariant();
        var rate = await currency.GetRateAsync(baseCurrency, targetCurrency.Trim().ToUpperInvariant(), ct);

        return new ProductPriceResponse
        {
            ProductId = id,
            BaseCurrency = baseCurrency,
            BasePrice = price,
            TargetCurrency = rate.TargetCurrency,
            Rate = rate.Rate,
            ConvertedPrice = Math.Round(price * rate.Rate, 2, MidpointRounding.AwayFromZero),
            RateRetrievedAtUtc = rate.RetrievedAtUtc
        };
    }

    private static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    private async Task EnsureSkuIsFreeAsync(string sku, int? exceptId, CancellationToken ct)
    {
        if (await db.Products.AnyAsync(p => p.Sku == sku && (exceptId == null || p.Id != exceptId), ct))
            throw new ConflictException($"A product with SKU '{sku}' already exists.");
    }

    private async Task EnsureReferencesExistAsync(ProductRequest request, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
            throw new NotFoundException(nameof(Category), request.CategoryId);

        if (request.SupplierId is { } supplierId && !await db.Suppliers.AnyAsync(s => s.Id == supplierId, ct))
            throw new NotFoundException(nameof(Supplier), supplierId);
    }
}
