using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/movies")]
public class MoviesController : CrudController<Movie, MovieDto>
{
    private readonly IMovieService _movieService;
    private readonly IMovieDiscoveryService _movieDiscoveryService;

    public MoviesController(
        IMovieService crudService,
        IMovieDiscoveryService movieDiscoveryService) : base(crudService)
    {
        _movieService = crudService;
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
        Guid? genreId = null;
        if (Request.Query.TryGetValue("genreId", out var genreStr) && Guid.TryParse(genreStr, out var parsedId))
        {
            genreId = parsedId;
        }

        return Ok(await _movieService.GetAllAsync(genreId, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public override async Task<ActionResult<MovieDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return await base.GetById(id, cancellationToken);
    }

    [HttpPut("{id:guid}")]
    [MovieBooking.Infrastructure.Security.HasPermission("Update")]
    public override async Task<IActionResult> Update(
        Guid id,
        [FromBody] MovieDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _movieService.UpdateWithEmbeddingAsync(id, dto, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
