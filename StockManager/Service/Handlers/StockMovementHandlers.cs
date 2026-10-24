using MediatR;
using StockManager.Abstract;
using StockManager.Models.CQRS.Commands;
using StockManager.Models.CQRS.Queries;
using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;

namespace StockManager.Service.Handlers;

public class RegisterStockMovementCommandHandler(IStockMovementService movements)
    : IRequestHandler<RegisterStockMovementCommand, StockMovementResponse>
{
    public Task<StockMovementResponse> Handle(RegisterStockMovementCommand request, CancellationToken ct) =>
        movements.RegisterAsync(request.Request, ct);
}

public class GetStockMovementsQueryHandler(IStockMovementService movements)
    : IRequestHandler<GetStockMovementsQuery, PagedResult<StockMovementResponse>>
{
    public Task<PagedResult<StockMovementResponse>> Handle(GetStockMovementsQuery request, CancellationToken ct) =>
        movements.GetPagedAsync(request.Filter, ct);
}
