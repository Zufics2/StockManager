using MediatR;
using StockManager.Models.DTOs;

namespace StockManager.Models.CQRS.Queries;

/// <summary>Product price converted with a live exchange rate (external API).</summary>
public record GetProductPriceQuery(int ProductId, string Currency) : IRequest<ProductPriceResponse>;
