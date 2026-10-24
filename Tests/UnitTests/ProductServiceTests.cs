using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using StockManager.Abstract;
using StockManager.Models.DTOs;
using StockManager.Models.Entities;
using StockManager.Models.Exceptions;
using StockManager.Models.Options;
using StockManager.Service.Data;
using StockManager.Service.Services;
using Tests.TestHelpers;

namespace Tests.UnitTests;

public class ProductServiceTests
{
    private readonly ApplicationDbContext _db = TestSetup.CreateDb();
    private readonly Mock<IFileService> _files = new();
    private readonly Mock<ICurrencyService> _currency = new();
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        _sut = new ProductService(
            _db,
            TestSetup.CreateMapper(),
            _files.Object,
            _currency.Object,
            Options.Create(new CurrencyOptions { BaseCurrency = "USD" }));
    }

    private async Task<(Category cat, Supplier sup)> SeedReferencesAsync()
    {
        var cat = new Category { Name = "Electronics" };
        var sup = new Supplier { Name = "Acme" };
        _db.AddRange(cat, sup);
        await _db.SaveChangesAsync();
        return (cat, sup);
    }

    private static ProductRequest Request(int categoryId, string sku = "abc-1", string name = "Cable", decimal price = 10m, int? supplierId = null) =>
        new() { Name = name, Sku = sku, Price = price, CategoryId = categoryId, SupplierId = supplierId };

    [Fact]
    public async Task Create_NormalizesSku_AndReturnsProjectedResponse()
    {
        var (cat, sup) = await SeedReferencesAsync();

        var result = await _sut.CreateAsync(Request(cat.Id, "  abc-1 ", supplierId: sup.Id));

        Assert.Equal("ABC-1", result.Sku);
        Assert.Equal("Electronics", result.CategoryName);
        Assert.Equal("Acme", result.SupplierName);
        Assert.Equal(0, result.TotalStock);
    }

    [Fact]
    public async Task Create_DuplicateSku_IgnoringCase_ThrowsConflict()
    {
        var (cat, _) = await SeedReferencesAsync();
        await _sut.CreateAsync(Request(cat.Id, "SKU-1"));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _sut.CreateAsync(Request(cat.Id, "sku-1")));

        Assert.Contains("SKU-1", ex.Message);
    }

    [Fact]
    public async Task Create_UnknownCategory_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateAsync(Request(categoryId: 999)));
    }

    [Fact]
    public async Task Create_UnknownSupplier_ThrowsNotFound()
    {
        var (cat, _) = await SeedReferencesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.CreateAsync(Request(cat.Id, supplierId: 999)));
    }

    [Fact]
    public async Task Create_WritesAuditLog_WithCurrentUser()
    {
        var (cat, _) = await SeedReferencesAsync();

        var product = await _sut.CreateAsync(Request(cat.Id, "AUD-1"));

        var log = Assert.Single(_db.AuditLogs.Where(l => l.EntityName == nameof(Product) && l.Action == "Create"));
        Assert.Equal(product.Id.ToString(), log.EntityId);
        Assert.Equal(TestSetup.UserId, log.UserId);
        Assert.Contains("AUD-1", log.Changes);
    }

    [Fact]
    public async Task Update_WritesAuditLog_WithOldAndNewValues()
    {
        var (cat, _) = await SeedReferencesAsync();
        var product = await _sut.CreateAsync(Request(cat.Id, "UPD-1", price: 10m));

        await _sut.UpdateAsync(product.Id, Request(cat.Id, "UPD-1", price: 12m));

        var log = Assert.Single(_db.AuditLogs.Where(l => l.EntityName == nameof(Product) && l.Action == "Update"));
        Assert.Contains("\"old\"", log.Changes);
        Assert.Contains("\"new\"", log.Changes);
    }

    [Fact]
    public async Task GetPaged_FiltersByCategoryAndPriceRange_AndSortsByPriceDescending()
    {
        var (cat, _) = await SeedReferencesAsync();
        var other = new Category { Name = "Books" };
        _db.Add(other);
        await _db.SaveChangesAsync();

        await _sut.CreateAsync(Request(cat.Id, "A-1", "Cheap", 5m));
        await _sut.CreateAsync(Request(cat.Id, "A-2", "Middle", 50m));
        await _sut.CreateAsync(Request(cat.Id, "A-3", "Pricey", 500m));
        await _sut.CreateAsync(Request(other.Id, "B-1", "Book", 20m));

        var page = await _sut.GetPagedAsync(new ProductFilterParameters
        {
            CategoryId = cat.Id,
            MinPrice = 10m,
            MaxPrice = 1000m,
            SortBy = "price",
            SortDirection = "desc"
        });

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { "Pricey", "Middle" }, page.Items.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task GetPaged_SearchesByName_CaseInsensitive_AndPages()
    {
        var (cat, _) = await SeedReferencesAsync();
        for (var i = 1; i <= 5; i++)
            await _sut.CreateAsync(Request(cat.Id, $"CBL-{i}", $"USB Cable {i}"));
        await _sut.CreateAsync(Request(cat.Id, "MSE-1", "Mouse"));

        var page = await _sut.GetPagedAsync(new ProductFilterParameters { Search = "cAbLe", Page = 2, PageSize = 2 });

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.Items.Count);
        Assert.True(page.HasPrevious);
        Assert.True(page.HasNext);
    }

    [Fact]
    public async Task Delete_ProductWithMovements_ThrowsConflict_AndKeepsProduct()
    {
        var (cat, _) = await SeedReferencesAsync();
        var product = await _sut.CreateAsync(Request(cat.Id, "DEL-1"));
        var warehouse = new Warehouse { Name = "W1" };
        _db.Add(warehouse);
        await _db.SaveChangesAsync();
        _db.StockMovements.Add(new StockMovement
        {
            ProductId = product.Id, WarehouseId = warehouse.Id, Type = MovementType.Incoming, Quantity = 1, QuantityAfter = 1
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => _sut.DeleteAsync(product.Id));

        Assert.True(_db.Products.Any(p => p.Id == product.Id));
    }

    [Fact]
    public async Task Delete_ProductWithImage_RemovesTheFile()
    {
        var (cat, _) = await SeedReferencesAsync();
        var product = await _sut.CreateAsync(Request(cat.Id, "IMG-0"));
        _db.Products.First(p => p.Id == product.Id).ImagePath = "/uploads/products/old.png";
        await _db.SaveChangesAsync();

        await _sut.DeleteAsync(product.Id);

        _files.Verify(f => f.Delete("/uploads/products/old.png"), Times.Once);
        Assert.Empty(_db.Products);
    }

    [Fact]
    public async Task SetImage_StoresNewFile_AndDeletesThePreviousOne()
    {
        var (cat, _) = await SeedReferencesAsync();
        var product = await _sut.CreateAsync(Request(cat.Id, "IMG-1"));
        _db.Products.First(p => p.Id == product.Id).ImagePath = "/uploads/products/old.png";
        await _db.SaveChangesAsync();

        var file = new Mock<IFormFile>().Object;
        _files.Setup(f => f.SaveImageAsync(file, It.IsAny<CancellationToken>())).ReturnsAsync("/uploads/products/new.png");

        var result = await _sut.SetImageAsync(product.Id, file);

        Assert.Equal("/uploads/products/new.png", result.ImageUrl);
        _files.Verify(f => f.Delete("/uploads/products/old.png"), Times.Once);
        Assert.Equal("/uploads/products/new.png", (await _sut.GetByIdAsync(product.Id)).ImageUrl);
    }

    [Fact]
    public async Task SetImage_WhenFileIsRejected_LeavesProductUntouched()
    {
        var (cat, _) = await SeedReferencesAsync();
        var product = await _sut.CreateAsync(Request(cat.Id, "IMG-2"));
        var file = new Mock<IFormFile>().Object;
        _files.Setup(f => f.SaveImageAsync(file, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BadRequestException("bad file"));

        await Assert.ThrowsAsync<BadRequestException>(() => _sut.SetImageAsync(product.Id, file));

        Assert.Null((await _sut.GetByIdAsync(product.Id)).ImageUrl);
        _files.Verify(f => f.Delete(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GetConvertedPrice_UsesBaseCurrencyAndRate()
    {
        var (cat, _) = await SeedReferencesAsync();
        var product = await _sut.CreateAsync(Request(cat.Id, "PRC-1", price: 10m));
        var retrieved = DateTime.UtcNow;
        _currency.Setup(c => c.GetRateAsync("USD", "KZT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExchangeRateResult("USD", "KZT", 500.126m, retrieved));

        var result = await _sut.GetConvertedPriceAsync(product.Id, "KZT");

        Assert.Equal("USD", result.BaseCurrency);
        Assert.Equal(5001.26m, result.ConvertedPrice);
        Assert.Equal(retrieved, result.RateRetrievedAtUtc);
    }

    [Fact]
    public async Task GetConvertedPrice_WhenExchangeProviderIsDown_PropagatesExternalServiceException()
    {
        var (cat, _) = await SeedReferencesAsync();
        var product = await _sut.CreateAsync(Request(cat.Id, "PRC-2"));
        _currency.Setup(c => c.GetRateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalServiceException("down"));

        await Assert.ThrowsAsync<ExternalServiceException>(() => _sut.GetConvertedPriceAsync(product.Id, "EUR"));
    }

    [Fact]
    public async Task GetById_Unknown_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetByIdAsync(12345));
    }
}
