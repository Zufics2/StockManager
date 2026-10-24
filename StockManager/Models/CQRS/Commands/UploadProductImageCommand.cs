using MediatR;
using StockManager.Models.DTOs;

namespace StockManager.Models.CQRS.Commands;

public record UploadProductImageCommand(int ProductId, IFormFile File) : IRequest<ProductImageResponse>;
