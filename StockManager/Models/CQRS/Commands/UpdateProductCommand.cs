using MediatR;
using StockManager.Models.DTOs;

namespace StockManager.Models.CQRS.Commands;

public record UpdateProductCommand(int Id, ProductRequest Request) : IRequest<ProductResponse>;
