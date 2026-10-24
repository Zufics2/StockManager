using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;

namespace StockManager.Abstract;

public interface IStockMovementService
{
    /// <summary>
    /// Registers a receipt or a write-off. For write-offs the available balance is verified first
    /// and <c>InsufficientStockException</c> is thrown when it is too low.
    /// </summary>
    Task<StockMovementResponse> RegisterAsync(RegisterStockMovementRequest request, CancellationToken ct = default);

    Task<PagedResult<StockMovementResponse>> GetPagedAsync(StockMovementFilterParameters filter, CancellationToken ct = default);
}
