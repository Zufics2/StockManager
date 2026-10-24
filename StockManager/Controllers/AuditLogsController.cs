using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockManager.Abstract;
using StockManager.Models.Constants;
using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;

namespace StockManager.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class AuditLogsController(IAuditLogService auditLogs) : ControllerBase
{
    /// <summary>Operations journal. Filters: userId, entityName, action, from, to, search.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuditLogResponse>>> GetAll([FromQuery] AuditLogFilterParameters filter, CancellationToken ct) =>
        Ok(await auditLogs.GetPagedAsync(filter, ct));
}
