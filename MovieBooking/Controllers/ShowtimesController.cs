using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/showtimes")]
public class ShowtimesController : CrudController<Showtime, ShowtimeDto>
{
    public ShowtimesController(ICrudService<Showtime, ShowtimeDto> crudService) : base(crudService) { }

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
}
