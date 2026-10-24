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

public class StockMovementService(
    ApplicationDbContext db,
    IMapper mapper,
    ICurrentUserService currentUser) : IStockMovementService
{
    public static readonly IReadOnlyDictionary<string, Expression<Func<StockMovement, object>>> SortFields =
        new Dictionary<string, Expression<Func<StockMovement, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdAt"] = m => m.CreatedAt,
            ["quantity"] = m => m.Quantity,
            ["productName"] = m => m.Product.Name
        };

    public async Task<StockMovementResponse> RegisterAsync(RegisterStockMovementRequest request, CancellationToken ct = default)
    {
        var product = await db.Products.AsNoTracking()
                          .Where(p => p.Id == request.ProductId)
                          .Select(p => new { p.Id, p.Name })
                          .FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var warehouse = await db.Warehouses.AsNoTracking()
                            .Where(w => w.Id == request.WarehouseId)
                            .Select(w => new { w.Id, w.Name })
                            .FirstOrDefaultAsync(ct)
                        ?? throw new NotFoundException(nameof(Warehouse), request.WarehouseId);

        if (request.Type == MovementType.Incoming && request.SupplierId is { } supplierId
                                                   && !await db.Suppliers.AnyAsync(s => s.Id == supplierId, ct))
            throw new NotFoundException(nameof(Supplier), supplierId);

        var level = await db.StockLevels.FirstOrDefaultAsync(
            l => l.ProductId == request.ProductId && l.WarehouseId == request.WarehouseId, ct);

        var now = DateTime.UtcNow;

        if (request.Type == MovementType.Incoming)
        {
            if (level is null)
            {
                level = new StockLevel { ProductId = product.Id, WarehouseId = warehouse.Id, Quantity = 0 };
                db.StockLevels.Add(level);
            }

            level.Quantity = checked(level.Quantity + request.Quantity);
        }
        else
        {
            // ---- availability check before the write-off ----
            var available = level?.Quantity ?? 0;
            if (level is null || available < request.Quantity)
                throw new InsufficientStockException(product.Name, warehouse.Name, request.Quantity, available);

            level.Quantity -= request.Quantity;
        }

        level.ConcurrencyStamp = Guid.NewGuid();
        level.UpdatedAt = now;

        var movement = new StockMovement
        {
            ProductId = product.Id,
            WarehouseId = warehouse.Id,
            Type = request.Type,
            Quantity = request.Quantity,
            QuantityAfter = level.Quantity,
            SupplierId = request.Type == MovementType.Incoming ? request.SupplierId : null,
            UnitCost = request.Type == MovementType.Incoming ? request.UnitCost : null,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            CreatedAt = now,
            CreatedByUserId = currentUser.UserId,
            CreatedByUserName = currentUser.UserName
        };
        db.StockMovements.Add(movement);

        try
        {
            // movement + new balance are written in ONE SaveChanges => one database transaction
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Somebody changed the same balance between our read and write: never overwrite it silently.
            throw new ConflictException(
                $"The stock of '{product.Name}' in '{warehouse.Name}' was modified by another operation. Please retry.");
        }
        catch (DbUpdateException)
        {
            // e.g. two first-ever receipts for the same product/warehouse racing on the composite key
            throw new ConflictException(
                $"The stock of '{product.Name}' in '{warehouse.Name}' was modified concurrently. Please retry.");
        }

        return await db.StockMovements.AsNoTracking()
            .Where(m => m.Id == movement.Id)
            .ProjectTo<StockMovementResponse>(mapper.ConfigurationProvider)
            .FirstAsync(ct);
    }

    public async Task<PagedResult<StockMovementResponse>> GetPagedAsync(StockMovementFilterParameters filter, CancellationToken ct = default)
    {
        IQueryable<StockMovement> query = db.StockMovements.AsNoTracking();

        if (filter.ProductId is { } productId)
            query = query.Where(m => m.ProductId == productId);

        if (filter.WarehouseId is { } warehouseId)
            query = query.Where(m => m.WarehouseId == warehouseId);

        if (filter.Type is { } type)
            query = query.Where(m => m.Type == type);

        if (filter.From is { } from)
        {
            var fromUtc = from.ToUtc();
            query = query.Where(m => m.CreatedAt >= fromUtc);
        }

        if (filter.To is { } to)
        {
            var toUtc = to.ToUtc();
            query = query.Where(m => m.CreatedAt <= toUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(m => m.Product.Name.ToLower().Contains(term)
                                     || m.Product.Sku.ToLower().Contains(term)
                                     || (m.Reason != null && m.Reason.ToLower().Contains(term)));
        }

        query = query.ApplySorting(filter.SortBy, filter.IsDescending(true), SortFields, "createdAt", m => m.Id);

        return await query.ToPagedResultAsync<StockMovement, StockMovementResponse>(mapper.ConfigurationProvider, filter.Page, filter.PageSize, ct);
    }
}
