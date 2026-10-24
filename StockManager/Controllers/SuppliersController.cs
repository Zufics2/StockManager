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
public class SuppliersController(ISupplierService suppliers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SupplierResponse>>> GetAll([FromQuery] SupplierFilterParameters filter, CancellationToken ct) =>
        Ok(await suppliers.GetPagedAsync(filter, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierResponse>> GetById(int id, CancellationToken ct) =>
        Ok(await suppliers.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.AdminOrManager)]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<SupplierResponse>> Create([FromBody] SupplierRequest request, CancellationToken ct)
    {
        var created = await suppliers.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<ActionResult<SupplierResponse>> Update(int id, [FromBody] SupplierRequest request, CancellationToken ct) =>
        Ok(await suppliers.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await suppliers.DeleteAsync(id, ct);
        return NoContent();
    }
}
