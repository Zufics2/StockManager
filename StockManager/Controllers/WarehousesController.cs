using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockManager.Abstract;
using StockManager.Models.Constants;
using StockManager.Models.DTOs;
using StockManager.Models.DTOs.Common;

namespace StockManager.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class WarehousesController(IWarehouseService warehouses) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<WarehouseResponse>>> GetAll([FromQuery] WarehouseFilterParameters filter, CancellationToken ct) =>
        Ok(await warehouses.GetPagedAsync(filter, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WarehouseResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await warehouses.GetByIdAsync(id, ct));

    /// <summary>Current stock balances of every product stored in the warehouse.</summary>
    [HttpGet("{id:int}/stock")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<StockLevelResponse>>> GetStock(int id, [FromQuery] StockLevelFilterParameters filter, CancellationToken ct) =>
        Ok(await warehouses.GetStockAsync(id, filter, ct));

    [HttpPost]
    [Authorize(Roles = Roles.AdminOrManager)]
    [ProducesResponseType(typeof(WarehouseResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseResponse>> Create([FromBody] WarehouseRequest request, CancellationToken ct)
    {
        var created = await warehouses.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<ActionResult<WarehouseResponse>> Update(int id, [FromBody] WarehouseRequest request, CancellationToken ct) =>
        Ok(await warehouses.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await warehouses.DeleteAsync(id, ct);
        return NoContent();
    }
}
