using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using StockManager.Abstract;
using StockManager.Models.Constants;
using StockManager.Models.DTOs;
using StockManager.Models.Entities;
using StockManager.Models.Exceptions;
using StockManager.Models.Options;
using StockManager.Service.Data;
using ValidationFailure = FluentValidation.Results.ValidationFailure;

namespace StockManager.Service.Services;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    IOptions<JwtOptions> jwtOptions,
    ICurrentUserService currentUser,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            throw ToException(created);
        
        await userManager.AddToRoleAsync(user, Roles.Viewer);
        await WriteAuditAsync("Register", user, null, ct);

        return await BuildResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        const string invalid = "Invalid email or password.";

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            throw new UnauthorizedException(invalid);

        if (await userManager.IsLockedOutAsync(user))
            throw new UnauthorizedException("The account is temporarily locked after too many failed attempts. Try again later.");

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("Failed login attempt for {Email} from {Ip}", request.Email, currentUser.IpAddress);
            throw new UnauthorizedException(invalid);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        await WriteAuditAsync("Login", user, null, ct);

        return await BuildResponseAsync(user);
    }

    public async Task<UserRoleResponse> AssignRoleAsync(string userId, string role, CancellationToken ct = default)
    {
        if (!Roles.All.Contains(role, StringComparer.OrdinalIgnoreCase))
            throw new BadRequestException($"Unknown role '{role}'. Allowed roles: {string.Join(", ", Roles.All)}.");

        role = Roles.All.First(r => string.Equals(r, role, StringComparison.OrdinalIgnoreCase));

        var user = await userManager.FindByIdAsync(userId) ?? throw new NotFoundException(nameof(ApplicationUser), userId);

        if (string.Equals(user.Id, currentUser.UserId, StringComparison.Ordinal))
            throw new BadRequestException("You cannot change your own role.");

        var oldRoles = await userManager.GetRolesAsync(user);
        var removed = await userManager.RemoveFromRolesAsync(user, oldRoles);
        if (!removed.Succeeded) throw ToException(removed);

        var added = await userManager.AddToRoleAsync(user, role);
        if (!added.Succeeded) throw ToException(added);

        var changes = JsonSerializer.Serialize(new { roles = new { old = oldRoles, @new = new[] { role } } });
        await WriteAuditAsync("AssignRole", user, changes, ct);

        return new UserRoleResponse { UserId = user.Id, Email = user.Email ?? string.Empty, Roles = [role] };
    }
    

    private async Task<AuthResponse> BuildResponseAsync(ApplicationUser user)
    {
        var jwt = jwtOptions.Value;
        var roles = await userManager.GetRolesAsync(user);
        var expires = DateTime.UtcNow.AddMinutes(jwt.ExpiryMinutes);
        
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", user.Email ?? user.UserName ?? user.Id)
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new AuthResponse
        {
            Token = token,
            ExpiresAtUtc = expires,
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            Roles = roles.ToList()
        };
    }

    private async Task WriteAuditAsync(string action, ApplicationUser user, string? changes, CancellationToken ct)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.UserId ?? user.Id,
            UserName = currentUser.UserName ?? user.Email,
            Action = action,
            EntityName = nameof(ApplicationUser),
            EntityId = user.Id,
            Changes = changes,
            IpAddress = currentUser.IpAddress,
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    private static Exception ToException(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code.StartsWith("Duplicate", StringComparison.Ordinal)))
            return new ConflictException("A user with this email already exists.");

        var failures = result.Errors.Select(e => new ValidationFailure(
            e.Code.StartsWith("Password", StringComparison.Ordinal) ? "Password" :
            e.Code.Contains("Email", StringComparison.Ordinal) || e.Code.Contains("UserName", StringComparison.Ordinal) ? "Email" : string.Empty,
            e.Description));

        return new ValidationException(failures);
    }
}
