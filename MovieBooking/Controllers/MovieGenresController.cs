using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Security;

namespace MovieBooking.Controllers;

[Route("api/movie-genres")]
public class MovieGenresController : CrudController<MovieGenre, MovieGenreDto>
{
    public MovieGenresController(IMovieGenreService crudService) : base(crudService) { }

    [HttpPost]
    [HasPermission("Create")]
    public override Task<ActionResult<MovieGenreDto>> Create(
        [FromBody] MovieGenreDto dto,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<ActionResult<MovieGenreDto>>(MutationNotAllowed());
    }

    [HttpPut("{id:guid}")]
    [HasPermission("Update")]
    public override Task<IActionResult> Update(
        Guid id,
        [FromBody] MovieGenreDto dto,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IActionResult>(MutationNotAllowed());
    }

    [HttpDelete("{id:guid}")]
    [HasPermission("Delete")]
    public override Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult<IActionResult>(MutationNotAllowed());
    }

    private ObjectResult MutationNotAllowed()
    {
        return StatusCode(
            StatusCodes.Status405MethodNotAllowed,
            new
            {
                message = "Movie genres must be changed through the movie create/update endpoints."
            });
    }
}
