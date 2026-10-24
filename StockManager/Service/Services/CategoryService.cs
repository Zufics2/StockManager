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

public class CategoryService(ApplicationDbContext db, IMapper mapper) : ICategoryService
{
    public static readonly IReadOnlyDictionary<string, Expression<Func<Category, object>>> SortFields =
        new Dictionary<string, Expression<Func<Category, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = c => c.Name,
            ["id"] = c => c.Id
        };

    public async Task<PagedResult<CategoryResponse>> GetPagedAsync(CategoryFilterParameters filter, CancellationToken ct = default)
    {
        IQueryable<Category> query = db.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term));
        }

        query = query.ApplySorting(filter.SortBy, filter.IsDescending(false), SortFields, "name", c => c.Id);
        return await query.ToPagedResultAsync<Category, CategoryResponse>(mapper.ConfigurationProvider, filter.Page, filter.PageSize, ct);
    }

    public async Task<CategoryResponse> GetByIdAsync(int id, CancellationToken ct = default) =>
        await db.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .ProjectTo<CategoryResponse>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException(nameof(Category), id);

    public async Task<CategoryResponse> CreateAsync(CategoryRequest request, CancellationToken ct = default)
    {
        await EnsureNameIsFreeAsync(request.Name, null, ct);

        var category = mapper.Map<Category>(request);
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(category.Id, ct);
    }

    public async Task<CategoryResponse> UpdateAsync(int id, CategoryRequest request, CancellationToken ct = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException(nameof(Category), id);

        await EnsureNameIsFreeAsync(request.Name, id, ct);

        mapper.Map(request, category);
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new NotFoundException(nameof(Category), id);

        if (await db.Products.AnyAsync(p => p.CategoryId == id, ct))
            throw new ConflictException($"Category '{category.Name}' still contains products and cannot be deleted.");

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureNameIsFreeAsync(string name, int? exceptId, CancellationToken ct)
    {
        var normalized = name.Trim().ToLower();
        var exists = await db.Categories.AnyAsync(c => c.Name.ToLower() == normalized && (exceptId == null || c.Id != exceptId), ct);
        if (exists)
            throw new ConflictException($"A category named '{name.Trim()}' already exists.");
    }
}
