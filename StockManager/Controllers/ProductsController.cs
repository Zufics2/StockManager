using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockManager.Models.Constants;
using StockManager.Models.CQRS.Commands;
using StockManager.Models.CQRS.Queries;
using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;
using StockManager.Models.Exceptions;

namespace StockManager.Controllers;

/// <summary>Products. Reads and writes go through MediatR queries / commands (CQRS).</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ProductsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Search, filter, sort and page products.
    /// Filters: search (name/SKU/description), categoryId, supplierId, minPrice, maxPrice, inStock.
    /// Sorting: sortBy = name | sku | price | createdAt, sortDirection = asc | desc.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProductResponse>>> GetAll([FromQuery] ProductFilterParameters filter, CancellationToken ct) =>
        Ok(await mediator.Send(new GetProductsQuery(filter), ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await mediator.Send(new GetProductByIdQuery(id), ct));

    /// <summary>Product price converted to another currency using a live exchange rate (external API + Polly).</summary>
    [HttpGet("{id:int}/price")]
    [ProducesResponseType(typeof(ProductPriceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ProductPriceResponse>> GetPrice(int id, [FromQuery] PriceConversionParameters query, CancellationToken ct) =>
        Ok(await mediator.Send(new GetProductPriceQuery(id, query.Currency), ct));

    [HttpPost]
    [Authorize(Roles = Roles.AdminOrManager)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Create([FromBody] ProductRequest request, CancellationToken ct)
    {
        var created = await mediator.Send(new CreateProductCommand(request), ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.AdminOrManager)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Update(int id, [FromBody] ProductRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateProductCommand(id, request), ct));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await mediator.Send(new DeleteProductCommand(id), ct);
        return NoContent();
    }

    /// <summary>Upload / replace the product image (jpg, jpeg, png, webp; max 2 MB by default).</summary>
    [HttpPost("{id:int}/image")]
    [Authorize(Roles = Roles.AdminOrManager)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(typeof(ProductImageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductImageResponse>> UploadImage(int id, IFormFile? file, CancellationToken ct)
    {
        if (file is null)
            throw new BadRequestException("No file was uploaded. Send the image in a multipart/form-data field named 'file'.");

        return Ok(await mediator.Send(new UploadProductImageCommand(id, file), ct));
    }
}
