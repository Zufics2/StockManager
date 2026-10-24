using MediatR;
using StockManager.Models.DTOs;

namespace StockManager.Models.CQRS.Commands;

/// <summary>Registers a stock receipt or a write-off (see <see cref="RegisterStockMovementRequest.Type"/>).</summary>
public record RegisterStockMovementCommand(RegisterStockMovementRequest Request) : IRequest<StockMovementResponse>;
