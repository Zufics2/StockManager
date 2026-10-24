using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;

namespace StockManager.Abstract;

public interface ICategoryService
{
    Task<PagedResult<CategoryResponse>> GetPagedAsync(CategoryFilterParameters filter, CancellationToken ct = default);
    Task<CategoryResponse> GetByIdAsync(int id, CancellationToken ct = default);
    Task<CategoryResponse> CreateAsync(CategoryRequest request, CancellationToken ct = default);
    Task<CategoryResponse> UpdateAsync(int id, CategoryRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
