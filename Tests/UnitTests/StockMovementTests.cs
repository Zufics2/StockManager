using Microsoft.EntityFrameworkCore;
using Moq;
using StockManager.Abstract;
using StockManager.Models.CQRS.Commands;
using StockManager.Models.DTOs;
using StockManager.Models.Entities;
using StockManager.Models.Exceptions;
using StockManager.Service.Data;
using StockManager.Service.Handlers;
using StockManager.Service.Services;
using Tests.TestHelpers;

namespace Tests.UnitTests;

public class StockMovementTests
{
    private readonly ApplicationDbContext _db = TestSetup.CreateDb();
    private readonly StockMovementService _sut;

    private int _productId;
    private int _warehouseId;
    private int _supplierId;

    public StockMovementTests()
    {
        _sut = new StockMovementService(_db, TestSetup.CreateMapper(), TestSetup.CurrentUser().Object);
        Seed();
    }

    private void Seed()
    {
        var category = new Category { Name = "Cat" };
        var supplier = new Supplier { Name = "Acme" };
        var warehouse = new Warehouse { Name = "Main" };
        var product = new Product { Name = "Widget", Sku = "WGT-1", Price = 3m, Category = category };
        _db.AddRange(category, supplier, warehouse, product);
        _db.SaveChanges();

        _productId = product.Id;
        _warehouseId = warehouse.Id;
        _supplierId = supplier.Id;
    }

    private static RegisterStockMovementRequest Receipt(int productId, int warehouseId, int qty, int? supplierId = null) =>
        new() { Type = MovementType.Incoming, ProductId = productId, WarehouseId = warehouseId, Quantity = qty, SupplierId = supplierId, UnitCost = 1.5m };

    private static RegisterStockMovementRequest WriteOff(int productId, int warehouseId, int qty) =>
        new() { Type = MovementType.Outgoing, ProductId = productId, WarehouseId = warehouseId, Quantity = qty, Reason = "Damaged" };

    private int Balance() =>
        _db.StockLevels.AsNoTracking()
            .Where(l => l.ProductId == _productId && l.WarehouseId == _warehouseId)
            .Select(l => l.Quantity)
            .SingleOrDefault();

    [Fact]
    public async Task Receipt_CreatesBalance_AndRecordsMovement()
    {
        var result = await _sut.RegisterAsync(Receipt(_productId, _warehouseId, 10, _supplierId));

        Assert.Equal(10, Balance());
        Assert.Equal(MovementType.Incoming, result.Type);
        Assert.Equal(10, result.QuantityAfter);
        Assert.Equal("Acme", result.SupplierName);
        Assert.Equal(TestSetup.UserName, result.CreatedBy);
    }

    [Fact]
    public async Task SecondReceipt_AddsToExistingBalance()
    {
        await _sut.RegisterAsync(Receipt(_productId, _warehouseId, 10));
        var second = await _sut.RegisterAsync(Receipt(_productId, _warehouseId, 5));

        Assert.Equal(15, Balance());
        Assert.Equal(15, second.QuantityAfter);
    }

    [Fact]
    public async Task WriteOff_WithEnoughStock_DecreasesBalance()
    {
        await _sut.RegisterAsync(Receipt(_productId, _warehouseId, 10));

        var result = await _sut.RegisterAsync(WriteOff(_productId, _warehouseId, 4));

        Assert.Equal(6, Balance());
        Assert.Equal(MovementType.Outgoing, result.Type);
        Assert.Equal(6, result.QuantityAfter);
        Assert.Equal("Damaged", result.Reason);
    }

    [Fact]
    public async Task WriteOff_ExactlyTheAvailableQuantity_IsAllowed()
    {
        await _sut.RegisterAsync(Receipt(_productId, _warehouseId, 7));

        await _sut.RegisterAsync(WriteOff(_productId, _warehouseId, 7));

        Assert.Equal(0, Balance());
    }

    [Fact]
    public async Task WriteOff_MoreThanAvailable_ThrowsInsufficientStock_AndChangesNothing()
    {
        await _sut.RegisterAsync(Receipt(_productId, _warehouseId, 3));

        var ex = await Assert.ThrowsAsync<InsufficientStockException>(() =>
            _sut.RegisterAsync(WriteOff(_productId, _warehouseId, 5)));

        Assert.Equal(5, ex.Requested);
        Assert.Equal(3, ex.Available);
        Assert.Contains("Widget", ex.Message);
        Assert.Equal(3, Balance());
        Assert.Equal(1, _db.StockMovements.Count());
    }

    [Fact]
    public async Task WriteOff_WhenProductNeverStockedInWarehouse_ThrowsInsufficientStockWithZeroAvailable()
    {
        var ex = await Assert.ThrowsAsync<InsufficientStockException>(() =>
            _sut.RegisterAsync(WriteOff(_productId, _warehouseId, 1)));

        Assert.Equal(0, ex.Available);
        Assert.Empty(_db.StockMovements);
    }

    [Fact]
    public async Task Stock_IsTrackedPerWarehouse()
    {
        var second = new Warehouse { Name = "Second" };
        _db.Add(second);
        await _db.SaveChangesAsync();

        await _sut.RegisterAsync(Receipt(_productId, _warehouseId, 10));

        // the other warehouse has nothing, so a write-off there must fail
        await Assert.ThrowsAsync<InsufficientStockException>(() =>
            _sut.RegisterAsync(WriteOff(_productId, second.Id, 1)));
    }

    [Fact]
    public async Task Receipt_UnknownProduct_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.RegisterAsync(Receipt(999, _warehouseId, 1)));

    [Fact]
    public async Task Receipt_UnknownWarehouse_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.RegisterAsync(Receipt(_productId, 999, 1)));

    [Fact]
    public async Task Receipt_UnknownSupplier_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.RegisterAsync(Receipt(_productId, _warehouseId, 1, supplierId: 999)));

    [Fact]
    public async Task History_CanBeFilteredByType_AndIsNewestFirstByDefault()
    {
        await _sut.RegisterAsync(Receipt(_productId, _warehouseId, 10));
        await _sut.RegisterAsync(WriteOff(_productId, _warehouseId, 2));
        await _sut.RegisterAsync(WriteOff(_productId, _warehouseId, 1));

        var outgoing = await _sut.GetPagedAsync(new StockMovementFilterParameters { Type = MovementType.Outgoing });
        var all = await _sut.GetPagedAsync(new StockMovementFilterParameters());

        Assert.Equal(2, outgoing.TotalCount);
        Assert.All(outgoing.Items, m => Assert.Equal(MovementType.Outgoing, m.Type));
        Assert.Equal(3, all.TotalCount);
        Assert.True(all.Items[0].Id > all.Items[^1].Id);
    }

    [Fact]
    public async Task Handler_DelegatesToTheService()
    {
        var expected = new StockMovementResponse { Id = 42, Quantity = 3 };
        var request = Receipt(_productId, _warehouseId, 3);
        var service = new Mock<IStockMovementService>();
        service.Setup(s => s.RegisterAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var handler = new RegisterStockMovementCommandHandler(service.Object);
        var result = await handler.Handle(new RegisterStockMovementCommand(request), CancellationToken.None);

        Assert.Same(expected, result);
        service.Verify(s => s.RegisterAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }
}
