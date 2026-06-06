using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/movies")]
public class MoviesController : CrudController<Movie, MovieDto>
{
    public MoviesController(ICrudService<Movie, MovieDto> crudService) : base(crudService) { }

    [AllowAnonymous]
    [HttpGet]
    public override async Task<ActionResult<IReadOnlyList<MovieDto>>> GetAll(CancellationToken cancellationToken)
    {
        return await base.GetAll(cancellationToken);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public override async Task<ActionResult<MovieDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return await base.GetById(id, cancellationToken);
    }
}
