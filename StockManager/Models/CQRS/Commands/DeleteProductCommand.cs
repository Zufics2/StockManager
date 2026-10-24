using MediatR;

namespace StockManager.Models.CQRS.Commands;

public record DeleteProductCommand(int Id) : IRequest;
