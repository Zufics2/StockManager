using System.Linq.Expressions;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using StockManager.Abstract;
using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;
using StockManager.Models.Entities;
using StockManager.Service.Data;
using StockManager.Service.Extensions;

namespace StockManager.Service.Services;

public class AuditLogService(ApplicationDbContext db, IMapper mapper) : IAuditLogService
{
    public static readonly IReadOnlyDictionary<string, Expression<Func<AuditLog, object>>> SortFields =
        new Dictionary<string, Expression<Func<AuditLog, object>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["timestamp"] = a => a.Timestamp,
            ["entityName"] = a => a.EntityName,
            ["action"] = a => a.Action,
            ["userName"] = a => a.UserName!
        };

    public async Task<PagedResult<AuditLogResponse>> GetPagedAsync(AuditLogFilterParameters filter, CancellationToken ct = default)
    {
        IQueryable<AuditLog> query = db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.UserId))
            query = query.Where(a => a.UserId == filter.UserId);

        if (!string.IsNullOrWhiteSpace(filter.EntityName))
        {
            var entity = filter.EntityName.Trim().ToLower();
            query = query.Where(a => a.EntityName.ToLower() == entity);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var action = filter.Action.Trim().ToLower();
            query = query.Where(a => a.Action.ToLower() == action);
        }

        if (filter.From is { } from)
        {
            var fromUtc = from.ToUtc();
            query = query.Where(a => a.Timestamp >= fromUtc);
        }

        if (filter.To is { } to)
        {
            var toUtc = to.ToUtc();
            query = query.Where(a => a.Timestamp <= toUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            query = query.Where(a => (a.UserName != null && a.UserName.ToLower().Contains(term))
                                     || (a.EntityId != null && a.EntityId.ToLower().Contains(term)));
        }

        query = query.ApplySorting(filter.SortBy, filter.IsDescending(true), SortFields, "timestamp", a => a.Id);
        return await query.ToPagedResultAsync<AuditLog, AuditLogResponse>(mapper.ConfigurationProvider, filter.Page, filter.PageSize, ct);
    }
}
