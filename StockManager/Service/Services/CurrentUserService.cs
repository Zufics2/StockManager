using System.Security.Claims;
using StockManager.Abstract;

namespace StockManager.Service.Services;

public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public string? UserId =>
        User?.FindFirst("sub")?.Value ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public string? UserName =>
        User?.FindFirst("name")?.Value ?? User?.Identity?.Name;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
