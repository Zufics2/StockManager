using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;

namespace StockManager.Abstract;

public interface IAuditLogService
{
    Task<PagedResult<AuditLogResponse>> GetPagedAsync(AuditLogFilterParameters filter, CancellationToken ct = default);
}
