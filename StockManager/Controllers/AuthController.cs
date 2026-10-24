using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StockManager.Abstract;
using StockManager.Models.Constants;
using StockManager.Models.DTOs;
using StockManager.Service.Extensions;
using ServiceCollectionExtensions = StockManager.Service.Extensions.ServiceCollectionExtensions;

namespace StockManager.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Creates an account. New users get the read-only "Viewer" role.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(ServiceCollectionExtensions.AuthRateLimitPolicy)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var response = await auth.RegisterAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>Exchanges email + password for a JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(ServiceCollectionExtensions.AuthRateLimitPolicy)]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct) =>
        Ok(await auth.LoginAsync(request, ct));

    /// <summary>Admin only: replaces the user's role (Admin, Manager, Storekeeper, Viewer).</summary>
    [HttpPut("users/{userId}/role")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(UserRoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserRoleResponse>> AssignRole(string userId, [FromBody] AssignRoleRequest request, CancellationToken ct) =>
        Ok(await auth.AssignRoleAsync(userId, request.Role, ct));
}
