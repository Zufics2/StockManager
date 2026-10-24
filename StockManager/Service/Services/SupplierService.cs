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

public class SupplierService(ApplicationDbContext db, IMapper mapper) : ISupplierService
{
    public static readonly IReadOnlyDictionary<string, Expression<Func<Supplier, object>>> SortFields =
        new Dictionary<string, Expression<Func<Supplier, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = s => s.Name,
            ["id"] = s => s.Id
        };

    public async Task<PagedResult<SupplierResponse>> GetPagedAsync(SupplierFilterParameters filter, CancellationToken ct = default)
    {
        IQueryable<Supplier> query = db.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(term)
                                     || (s.ContactEmail != null && s.ContactEmail.ToLower().Contains(term)));
        }

        query = query.ApplySorting(filter.SortBy, filter.IsDescending(false), SortFields, "name", s => s.Id);
        return await query.ToPagedResultAsync<Supplier, SupplierResponse>(mapper.ConfigurationProvider, filter.Page, filter.PageSize, ct);
    }

    public async Task<SupplierResponse> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Suppliers.AsNoTracking()
            .Where(s => s.Id == id)
            .ProjectTo<SupplierResponse>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(Supplier), id);

    public async Task<SupplierResponse> CreateAsync(SupplierRequest request, CancellationToken ct = default)
    {
        var supplier = mapper.Map<Supplier>(request);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(supplier.Id, ct);
    }

    public async Task<SupplierResponse> UpdateAsync(int id, SupplierRequest request, CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
                       ?? throw new NotFoundException(nameof(Supplier), id);

        mapper.Map(request, supplier);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
                       ?? throw new NotFoundException(nameof(Supplier), id);

        // Products / movements keep existing; their SupplierId is set to NULL by the FK rule.
        db.Suppliers.Remove(supplier);
        await db.SaveChangesAsync(ct);
    }
}
