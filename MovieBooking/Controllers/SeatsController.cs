using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/seats")]
public class SeatsController : CrudController<Seat, SeatDto>
{
    public SeatsController(ICrudService<Seat, SeatDto> crudService) : base(crudService) { }

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
}
