using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;

namespace StockManager.Abstract;

public interface IProductService
{
    Task<PagedResult<ProductResponse>> GetPagedAsync(ProductFilterParameters filter, CancellationToken ct = default);
    Task<ProductResponse> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ProductResponse> CreateAsync(ProductRequest request, CancellationToken ct = default);
    Task<ProductResponse> UpdateAsync(int id, ProductRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<ProductImageResponse> SetImageAsync(int id, IFormFile file, CancellationToken ct = default);
    Task<ProductPriceResponse> GetConvertedPriceAsync(int id, string currency, CancellationToken ct = default);
}
