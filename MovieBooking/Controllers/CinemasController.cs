using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/cinemas")]
public class CinemasController : CrudController<Cinema, CinemaDto>
{
    public CinemasController(ICrudService<Cinema, CinemaDto> crudService) : base(crudService) { }

    [AllowAnonymous]
    [HttpGet]
    public override async Task<ActionResult<IReadOnlyList<CinemaDto>>> GetAll(CancellationToken cancellationToken)
    {
        return await base.GetAll(cancellationToken);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public override async Task<ActionResult<CinemaDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return await base.GetById(id, cancellationToken);
    }
}
