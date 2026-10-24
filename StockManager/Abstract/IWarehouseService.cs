using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;

namespace StockManager.Abstract;

public interface IWarehouseService
{
    Task<PagedResult<WarehouseResponse>> GetPagedAsync(WarehouseFilterParameters filter, CancellationToken ct = default);
    Task<WarehouseResponse> GetByIdAsync(int id, CancellationToken ct = default);
    Task<WarehouseResponse> CreateAsync(WarehouseRequest request, CancellationToken ct = default);
    Task<WarehouseResponse> UpdateAsync(int id, WarehouseRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Current stock balances of a warehouse.</summary>
    Task<PagedResult<StockLevelResponse>> GetStockAsync(int warehouseId, StockLevelFilterParameters filter, CancellationToken ct = default);
}
