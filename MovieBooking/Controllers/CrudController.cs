using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Common;

namespace MovieBooking.Controllers;

[ApiController]
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
    public async Task<ActionResult<IReadOnlyList<TDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _crudService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _crudService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<TDto>> Create([FromBody] TDto dto, CancellationToken cancellationToken)
    {
        var created = await _crudService.CreateAsync(dto, cancellationToken);
        var id = (Guid?)typeof(TDto).GetProperty("Id")?.GetValue(created) ?? Guid.Empty;
        return CreatedAtAction(nameof(GetById), new { id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TDto dto, CancellationToken cancellationToken)
    {
        var updated = await _crudService.UpdateAsync(id, dto, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _crudService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
