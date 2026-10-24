using System.Linq.Expressions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using StockManager.Abstract;
using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;
using StockManager.Models.Entities;
using StockManager.Models.Exceptions;
using StockManager.Service.Data;
using StockManager.Service.Extensions;

namespace StockManager.Service.Services;

public class WarehouseService(ApplicationDbContext db, IMapper mapper) : IWarehouseService
{
    public static readonly IReadOnlyDictionary<string, Expression<Func<Warehouse, object>>> SortFields =
        new Dictionary<string, Expression<Func<Warehouse, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = w => w.Name,
            ["id"] = w => w.Id
        };

    public static readonly IReadOnlyDictionary<string, Expression<Func<StockLevel, object>>> StockSortFields =
        new Dictionary<string, Expression<Func<StockLevel, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["productName"] = l => l.Product.Name,
            ["sku"] = l => l.Product.Sku,
            ["quantity"] = l => l.Quantity
        };

    public async Task<PagedResult<WarehouseResponse>> GetPagedAsync(WarehouseFilterParameters filter, CancellationToken ct = default)
    {
        IQueryable<Warehouse> query = db.Warehouses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(w => w.Name.ToLower().Contains(term)
                                     || (w.Location != null && w.Location.ToLower().Contains(term)));
        }

        query = query.ApplySorting(filter.SortBy, filter.IsDescending(false), SortFields, "name", w => w.Id);
        return await query.ToPagedResultAsync<Warehouse, WarehouseResponse>(mapper.ConfigurationProvider, filter.Page, filter.PageSize, ct);
    }

    public async Task<WarehouseResponse> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Warehouses.AsNoTracking()
            .Where(w => w.Id == id)
            .ProjectTo<WarehouseResponse>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(Warehouse), id);

    public async Task<WarehouseResponse> CreateAsync(WarehouseRequest request, CancellationToken ct = default)
    {
        await EnsureNameIsFreeAsync(request.Name, null, ct);

        var warehouse = mapper.Map<Warehouse>(request);
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(warehouse.Id, ct);
    }

    public async Task<WarehouseResponse> UpdateAsync(int id, WarehouseRequest request, CancellationToken ct = default)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, ct)
                        ?? throw new NotFoundException(nameof(Warehouse), id);

        await EnsureNameIsFreeAsync(request.Name, id, ct);

        mapper.Map(request, warehouse);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var warehouse = await db.Warehouses.FirstOrDefaultAsync(w => w.Id == id, ct)
                        ?? throw new NotFoundException(nameof(Warehouse), id);

        if (await db.StockMovements.AnyAsync(m => m.WarehouseId == id, ct))
            throw new ConflictException($"Warehouse '{warehouse.Name}' has stock movements and cannot be deleted.");

        db.Warehouses.Remove(warehouse);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<StockLevelResponse>> GetStockAsync(int warehouseId, StockLevelFilterParameters filter, CancellationToken ct = default)
    {
        if (!await db.Warehouses.AnyAsync(w => w.Id == warehouseId, ct))
            throw new NotFoundException(nameof(Warehouse), warehouseId);

        IQueryable<StockLevel> query = db.StockLevels.AsNoTracking().Where(l => l.WarehouseId == warehouseId);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(l => l.Product.Name.ToLower().Contains(term) || l.Product.Sku.ToLower().Contains(term));
        }

        query = query.ApplySorting(filter.SortBy, filter.IsDescending(false), StockSortFields, "productName", l => l.ProductId);
        return await query.ToPagedResultAsync<StockLevel, StockLevelResponse>(mapper.ConfigurationProvider, filter.Page, filter.PageSize, ct);
    }

    private async Task EnsureNameIsFreeAsync(string name, int? exceptId, CancellationToken ct)
    {
        var normalized = name.Trim().ToLower();
        var exists = await db.Warehouses.AnyAsync(w => w.Name.ToLower() == normalized && (exceptId == null || w.Id != exceptId), ct);
        if (exists)
            throw new ConflictException($"A warehouse named '{name.Trim()}' already exists.");
    }
}
