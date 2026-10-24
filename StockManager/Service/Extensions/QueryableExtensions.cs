using System.Linq.Expressions;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using StockManager.Models.DTOs.Common;

namespace StockManager.Service.Extensions;

public static class QueryableExtensions
{
    /// <summary>
    /// Sorts by a whitelisted field (never by raw user input) and adds a unique tie-breaker so paging is stable.
    /// </summary>
    public static IQueryable<T> ApplySorting<T>(
        this IQueryable<T> query,
        string? sortBy,
        bool descending,
        IReadOnlyDictionary<string, Expression<Func<T, object>>> sortFields,
        string defaultField,
        Expression<Func<T, object>> tieBreaker)
    {
        var key = !string.IsNullOrWhiteSpace(sortBy) && sortFields.ContainsKey(sortBy) ? sortBy : defaultField;
        var selector = sortFields[key];

        var ordered = descending ? query.OrderByDescending(selector) : query.OrderBy(selector);
        return ordered.ThenBy(tieBreaker);
    }

    /// <summary>Counts, pages and projects to the DTO in the database (no entity materialisation).</summary>
    public static async Task<PagedResult<TDestination>> ToPagedResultAsync<TSource, TDestination>(
        this IQueryable<TSource> query,
        AutoMapper.IConfigurationProvider mapperConfiguration,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ProjectTo<TDestination>(mapperConfiguration)
            .ToListAsync(ct);

        return new PagedResult<TDestination>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }
}
