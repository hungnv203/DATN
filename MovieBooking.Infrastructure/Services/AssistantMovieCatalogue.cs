using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public sealed class AssistantMovieCatalogue : IAssistantMovieCatalogue
{
    private readonly AppDbContext _db;

    public AssistantMovieCatalogue(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AssistantMovieCandidateDto>> GetCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Movies
            .AsNoTracking()
            .Where(movie => movie.Status != "Inactive")
            .OrderByDescending(movie => movie.ReleaseDate)
            .Take(100)
            .Select(movie => new AssistantMovieCandidateDto
            {
                Id = movie.Id,
                Title = movie.Title,
                Description = movie.Description,
                Duration = movie.Duration,
                ReleaseDate = movie.ReleaseDate,
                Language = movie.Language,
                Rating = movie.Rating,
                PosterUrl = movie.PosterUrl,
                Status = movie.Status,
                Genres = movie.MovieGenres
                    .Select(movieGenre => movieGenre.Genre.Name)
                    .OrderBy(name => name)
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }
}
