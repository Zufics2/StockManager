using MediatR;
using StockManager.Models.DTOs;

namespace StockManager.Models.CQRS.Commands;

public record CreateProductCommand(ProductRequest Request) : IRequest<ProductResponse>;
