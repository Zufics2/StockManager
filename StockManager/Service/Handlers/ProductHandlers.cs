using MediatR;
using StockManager.Abstract;
using StockManager.Models.CQRS.Commands;
using StockManager.Models.CQRS.Queries;
using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;

namespace StockManager.Service.Handlers;

// ---------------- Queries (read side) ----------------

public class GetProductsQueryHandler(IProductService products)
    : IRequestHandler<GetProductsQuery, PagedResult<ProductResponse>>
{
    public Task<PagedResult<ProductResponse>> Handle(GetProductsQuery request, CancellationToken ct) =>
        products.GetPagedAsync(request.Filter, ct);
}

public class GetProductByIdQueryHandler(IProductService products)
    : IRequestHandler<GetProductByIdQuery, ProductResponse>
{
    public Task<ProductResponse> Handle(GetProductByIdQuery request, CancellationToken ct) =>
        products.GetByIdAsync(request.Id, ct);
}

public class GetProductPriceQueryHandler(IProductService products)
    : IRequestHandler<GetProductPriceQuery, ProductPriceResponse>
{
    public Task<ProductPriceResponse> Handle(GetProductPriceQuery request, CancellationToken ct) =>
        products.GetConvertedPriceAsync(request.ProductId, request.Currency, ct);
}

// ---------------- Commands (write side) ----------------

public class CreateProductCommandHandler(IProductService products)
    : IRequestHandler<CreateProductCommand, ProductResponse>
{
    public Task<ProductResponse> Handle(CreateProductCommand request, CancellationToken ct) =>
        products.CreateAsync(request.Request, ct);
}

public class UpdateProductCommandHandler(IProductService products)
    : IRequestHandler<UpdateProductCommand, ProductResponse>
{
    public Task<ProductResponse> Handle(UpdateProductCommand request, CancellationToken ct) =>
        products.UpdateAsync(request.Id, request.Request, ct);
}

public class DeleteProductCommandHandler(IProductService products)
    : IRequestHandler<DeleteProductCommand>
{
    public Task Handle(DeleteProductCommand request, CancellationToken ct) =>
        products.DeleteAsync(request.Id, ct);
}

public class UploadProductImageCommandHandler(IProductService products)
    : IRequestHandler<UploadProductImageCommand, ProductImageResponse>
{
    public Task<ProductImageResponse> Handle(UploadProductImageCommand request, CancellationToken ct) =>
        products.SetImageAsync(request.ProductId, request.File, ct);
}
