using MediatR;
using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;

namespace StockManager.Models.CQRS.Queries;

public record GetStockMovementsQuery(StockMovementFilterParameters Filter) : IRequest<PagedResult<StockMovementResponse>>;
