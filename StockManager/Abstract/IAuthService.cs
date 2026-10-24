using StockManager.Models.DTOs;

namespace StockManager.Abstract;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserRoleResponse> AssignRoleAsync(string userId, string role, CancellationToken ct = default);
}
