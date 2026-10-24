using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;

namespace StockManager.Abstract;

public interface ISupplierService
{
    Task<PagedResult<SupplierResponse>> GetPagedAsync(SupplierFilterParameters filter, CancellationToken ct = default);
    Task<SupplierResponse> GetByIdAsync(int id, CancellationToken ct = default);
    Task<SupplierResponse> CreateAsync(SupplierRequest request, CancellationToken ct = default);
    Task<SupplierResponse> UpdateAsync(int id, SupplierRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
