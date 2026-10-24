using MediatR;
using StockManager.Models.DTOs;

namespace StockManager.Models.CQRS.Queries;

public record GetProductByIdQuery(int Id) : IRequest<ProductResponse>;
