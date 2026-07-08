using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Common;
using MovieBooking.Infrastructure.Security;

namespace MovieBooking.Controllers;

[ApiController]
[Authorize]
public abstract class CrudController<TEntity, TDto> : ControllerBase
    where TEntity : BaseEntity, new()
    where TDto : class, new()
{
    private readonly ICrudService<TEntity, TDto> _crudService;

    protected CrudController(ICrudService<TEntity, TDto> crudService)
    {
        _crudService = crudService;
    }

    [HttpGet]
    public virtual async Task<ActionResult<IReadOnlyList<TDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _crudService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public virtual async Task<ActionResult<TDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _crudService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [HasPermission("Create")]
    public virtual async Task<ActionResult<TDto>> Create([FromBody] TDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _crudService.CreateAsync(dto, cancellationToken);
            var id = (Guid?)typeof(TDto).GetProperty("Id")?.GetValue(created) ?? Guid.Empty;
            return CreatedAtAction(nameof(GetById), new { id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [HasPermission("Update")]
    public virtual async Task<IActionResult> Update(Guid id, [FromBody] TDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _crudService.UpdateAsync(id, dto, cancellationToken);
            return updated ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("Delete")]
    public virtual async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _crudService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
