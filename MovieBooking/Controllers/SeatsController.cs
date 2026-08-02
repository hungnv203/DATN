using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Security;

namespace MovieBooking.Controllers;

[Route("api/seats")]
public class SeatsController : CrudController<Seat, SeatDto>
{
    private readonly ISeatLayoutService _seatLayoutService;

    public SeatsController(
        ISeatService crudService,
        ISeatLayoutService seatLayoutService) : base(crudService)
    {
        _seatLayoutService = seatLayoutService;
    }

    [AllowAnonymous]
    [HttpGet]
    public override async Task<ActionResult<IReadOnlyList<SeatDto>>> GetAll(CancellationToken cancellationToken)
    {
        return await base.GetAll(cancellationToken);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public override async Task<ActionResult<SeatDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return await base.GetById(id, cancellationToken);
    }

    [HttpPost("bulk")]
    [HasPermission("Create")]
    public async Task<ActionResult<IReadOnlyList<SeatDto>>> CreateBulk(
        [FromBody] BulkSeatLayoutDto layout,
        CancellationToken cancellationToken)
    {
        try
        {
            var seats = await _seatLayoutService.CreateBulkAsync(layout, cancellationToken);
            return Ok(seats);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}

