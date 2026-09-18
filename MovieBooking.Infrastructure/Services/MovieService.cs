using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

internal sealed class MovieService : IMovieService
{
    private readonly EntityCrudOperations<Movie, MovieDto> _operations;
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public MovieService(AppDbContext dbContext, IMapper mapper)
    {
        _operations = new EntityCrudOperations<Movie, MovieDto>(dbContext, mapper);
        _db = dbContext;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<MovieDto>> GetAllAsync(Guid? genreId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.Movies
            .AsNoTracking()
            .Include(m => m.MovieGenres)
                .ThenInclude(mg => mg.Genre)
            .AsQueryable();

        if (genreId.HasValue && genreId.Value != Guid.Empty)
        {
            query = query.Where(m => m.MovieGenres.Any(mg => mg.GenreId == genreId.Value));
        }

        var movies = await query.OrderByDescending(m => m.ReleaseDate).ToListAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<MovieDto>>(movies);
    }

    public Task<IReadOnlyList<MovieDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        GetAllAsync(null, cancellationToken);

    public async Task<MovieDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var movie = await _db.Movies
            .AsNoTracking()
            .Include(m => m.MovieGenres)
                .ThenInclude(mg => mg.Genre)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        return movie is null ? null : _mapper.Map<MovieDto>(movie);
    }

    public async Task<MovieDto> CreateAsync(MovieDto dto, CancellationToken cancellationToken = default)
    {
        var movie = _mapper.Map<Movie>(dto);
        await _db.Movies.AddAsync(movie, cancellationToken);

        if (dto.GenreIds != null && dto.GenreIds.Count > 0)
        {
            var validGenreIds = await _db.Genres
                .Where(g => dto.GenreIds.Contains(g.Id))
                .Select(g => g.Id)
                .ToListAsync(cancellationToken);

            foreach (var gid in validGenreIds.Distinct())
            {
                movie.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = gid });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetByIdAsync(movie.Id, cancellationToken))!;
    }

    public async Task<bool> UpdateAsync(Guid id, MovieDto dto, CancellationToken cancellationToken = default)
    {
        var movie = await _db.Movies
            .Include(m => m.MovieGenres)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (movie is null) return false;

        _mapper.Map(dto, movie);

        if (dto.GenreIds != null)
        {
            _db.MovieGenres.RemoveRange(movie.MovieGenres);
            movie.MovieGenres.Clear();

            if (dto.GenreIds.Count > 0)
            {
                var validGenreIds = await _db.Genres
                    .Where(g => dto.GenreIds.Contains(g.Id))
                    .Select(g => g.Id)
                    .ToListAsync(cancellationToken);

                foreach (var gid in validGenreIds.Distinct())
                {
                    movie.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = gid });
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.DeleteAsync(id, cancellationToken);
}

