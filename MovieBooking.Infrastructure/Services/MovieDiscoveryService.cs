using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class MovieDiscoveryService : IMovieDiscoveryService
{
    private readonly AppDbContext _db;

    public MovieDiscoveryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<MovieDiscoveryDto> GetDiscoveryAsync(
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 20);
        var now = DateTimeOffset.UtcNow;
        var weekStart = now.AddDays(-7);
        var newReleaseStart = now.AddDays(-14).UtcDateTime.Date;
        var today = now.UtcDateTime.Date;

        var recentSales = await _db.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status == "Paid" && booking.CreatedAt >= weekStart)
            .GroupBy(booking => booking.Showtime.MovieId)
            .Select(group => new
            {
                MovieId = group.Key,
                TicketCount = group.SelectMany(booking => booking.Tickets).Count()
            })
            .ToListAsync(cancellationToken);

        var totalSales = await _db.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status == "Paid")
            .GroupBy(booking => booking.Showtime.MovieId)
            .Select(group => new
            {
                MovieId = group.Key,
                TicketCount = group.SelectMany(booking => booking.Tickets).Count()
            })
            .ToListAsync(cancellationToken);

        var recentSalesByMovie = recentSales.ToDictionary(item => item.MovieId, item => item.TicketCount);
        var totalSalesByMovie = totalSales.ToDictionary(item => item.MovieId, item => item.TicketCount);

        var releasedMovieIds = await _db.Movies
            .AsNoTracking()
            .Where(movie => movie.ReleaseDate <= today && movie.Status != "Inactive")
            .Select(movie => movie.Id)
            .ToListAsync(cancellationToken);

        var featuredIds = releasedMovieIds
            .OrderByDescending(movieId =>
                recentSalesByMovie.GetValueOrDefault(movieId) * 3
                + totalSalesByMovie.GetValueOrDefault(movieId))
            .Take(limit)
            .ToList();

        var trendingIds = recentSales
            .OrderByDescending(item => item.TicketCount)
            .Select(item => item.MovieId)
            .Take(limit)
            .ToList();
        var topRatedIds = trendingIds;
        var bestSellingIds = totalSales
            .OrderByDescending(item => item.TicketCount)
            .Select(item => item.MovieId)
            .Take(limit)
            .ToList();
        var newReleaseIds = await _db.Movies
            .AsNoTracking()
            .Where(movie => movie.ReleaseDate >= newReleaseStart
                && movie.ReleaseDate <= today
                && movie.Status != "Inactive")
            .OrderByDescending(movie => movie.ReleaseDate)
            .Select(movie => movie.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
        var upcomingIds = await _db.Movies
            .AsNoTracking()
            .Where(movie => movie.ReleaseDate > today || movie.Status == "Upcoming")
            .OrderBy(movie => movie.ReleaseDate)
            .Select(movie => movie.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new MovieDiscoveryDto
        {
            Featured = await LoadMoviesInOrderAsync(featuredIds, cancellationToken),
            Trending = await LoadMoviesInOrderAsync(trendingIds, cancellationToken),
            TopRated = await LoadMoviesInOrderAsync(topRatedIds, cancellationToken),
            BestSelling = await LoadMoviesInOrderAsync(bestSellingIds, cancellationToken),
            NewReleases = await LoadMoviesInOrderAsync(newReleaseIds, cancellationToken),
            Upcoming = await LoadMoviesInOrderAsync(upcomingIds, cancellationToken)
        };
    }

    private async Task<IReadOnlyList<MovieDto>> LoadMoviesInOrderAsync(
        IReadOnlyList<Guid> movieIds,
        CancellationToken cancellationToken)
    {
        if (movieIds.Count == 0)
        {
            return [];
        }

        var movies = await _db.Movies
            .AsNoTracking()
            .Where(movie => movieIds.Contains(movie.Id))
            .Select(movie => new MovieDto
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
                Genres = movie.MovieGenres.Select(mg => mg.Genre.Name).OrderBy(n => n).ToList(),
                GenreIds = movie.MovieGenres.Select(mg => mg.GenreId).ToList()
            })
            .ToListAsync(cancellationToken);
        var moviesById = movies.ToDictionary(movie => movie.Id);

        return movieIds
            .Where(moviesById.ContainsKey)
            .Select(movieId => moviesById[movieId])
            .ToList();
    }
}

