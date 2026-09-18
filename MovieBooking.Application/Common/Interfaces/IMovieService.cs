using MovieBooking.Application.Common.DTOs;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Application.Common.Interfaces;

public interface IMovieService : ICrudService<Movie, MovieDto>
{
    Task<IReadOnlyList<MovieDto>> GetAllAsync(Guid? genreId = null, CancellationToken cancellationToken = default);
}

