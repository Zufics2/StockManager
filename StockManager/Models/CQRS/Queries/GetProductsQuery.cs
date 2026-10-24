using MediatR;
using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;

namespace StockManager.Models.CQRS.Queries;

public record GetProductsQuery(ProductFilterParameters Filter) : IRequest<PagedResult<ProductResponse>>;
