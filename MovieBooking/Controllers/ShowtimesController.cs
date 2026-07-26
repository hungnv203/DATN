using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/showtimes")]
public class ShowtimesController : CrudController<Showtime, ShowtimeDto>
{
    private readonly IShowtimeService _showtimeService;

    public ShowtimesController(IShowtimeService showtimeService) : base(showtimeService)
    {
        _showtimeService = showtimeService;
    }

    [AllowAnonymous]
    [HttpGet]
    public override async Task<ActionResult<IReadOnlyList<ShowtimeDto>>> GetAll(CancellationToken cancellationToken)
    {
        return await base.GetAll(cancellationToken);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public override async Task<ActionResult<ShowtimeDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return await base.GetById(id, cancellationToken);
    }

    [HttpPost]
    public override async Task<ActionResult<ShowtimeDto>> Create([FromBody] ShowtimeDto dto, CancellationToken cancellationToken)
    {
        try
        {
            return await base.Create(dto, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public override async Task<IActionResult> Update(Guid id, [FromBody] ShowtimeDto dto, CancellationToken cancellationToken)
    {
        try
        {
            return await base.Update(id, dto, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpGet("{showtimeId:guid}/seats")]
    public async Task<ActionResult<IReadOnlyList<ShowtimeSeatDto>>> GetSeatsForShowtime(Guid showtimeId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _showtimeService.GetSeatsForShowtimeAsync(showtimeId, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Unable to load showtime seats.");
        }
    }

    [AllowAnonymous]
    [HttpGet("{showtimeId:guid}/seats/{seatId:guid}")]
    public async Task<ActionResult<ShowtimeSeatDto>> GetSeatForShowtime(
        Guid showtimeId,
        Guid seatId,
        CancellationToken cancellationToken)
    {
        var result = await _showtimeService.GetSeatForShowtimeAsync(
            showtimeId,
            seatId,
            cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }
}
