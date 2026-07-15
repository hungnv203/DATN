using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/movies")]
public class MoviesController : CrudController<Movie, MovieDto>
{
    private readonly IMovieDiscoveryService _movieDiscoveryService;

    public MoviesController(
        ICrudService<Movie, MovieDto> crudService,
        IMovieDiscoveryService movieDiscoveryService) : base(crudService)
    {
        _movieDiscoveryService = movieDiscoveryService;
    }

    [AllowAnonymous]
    [HttpGet("discovery")]
    public async Task<ActionResult<MovieDiscoveryDto>> GetDiscovery(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _movieDiscoveryService.GetDiscoveryAsync(limit, cancellationToken));
    }

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
