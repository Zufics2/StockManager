using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockManager.Models.Constants;
using StockManager.Models.CQRS.Commands;
using StockManager.Models.CQRS.Queries;
using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;

namespace StockManager.Controllers;

[ApiController]
[Route("api/stock-movements")]
[Authorize]
[Produces("application/json")]
public class StockMovementsController(IMediator mediator, IMapper mapper) : ControllerBase
{
    /// <summary>Movement history. Filters: productId, warehouseId, type, from, to, search.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<StockMovementResponse>>> GetAll([FromQuery] StockMovementFilterParameters filter, CancellationToken ct) =>
        Ok(await mediator.Send(new GetStockMovementsQuery(filter), ct));

    /// <summary>Registers a stock receipt (incoming goods).</summary>
    [HttpPost("receipts")]
    [Authorize(Roles = Roles.StockOperators)]
    [ProducesResponseType(typeof(StockMovementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockMovementResponse>> Receive([FromBody] StockReceiptRequest request, CancellationToken ct)
    {
        var command = new RegisterStockMovementCommand(mapper.Map<RegisterStockMovementRequest>(request));
        var movement = await mediator.Send(command, ct);
        return Created($"/api/stock-movements?productId={movement.ProductId}&warehouseId={movement.WarehouseId}", movement);
    }

    /// <summary>
    /// Registers a write-off / disposal (outgoing goods). The available stock is verified first;
    /// when it is too low the request is rejected with 409 and nothing is changed.
    /// </summary>
    [HttpPost("write-offs")]
    [Authorize(Roles = Roles.StockOperators)]
    [ProducesResponseType(typeof(StockMovementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockMovementResponse>> WriteOff([FromBody] StockWriteOffRequest request, CancellationToken ct)
    {
        var command = new RegisterStockMovementCommand(mapper.Map<RegisterStockMovementRequest>(request));
        var movement = await mediator.Send(command, ct);
        return Created($"/api/stock-movements?productId={movement.ProductId}&warehouseId={movement.WarehouseId}", movement);
    }
}
