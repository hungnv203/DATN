using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MovieBooking.Application.Common.Configuration;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Constants;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

internal sealed class MovieService : IMovieService
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    private readonly IEmbeddingSyncService _embeddingSyncService;
    private readonly MovieEmbeddingRetryOptions _retryOptions;
    private readonly TimeProvider _timeProvider;

    public MovieService(
        AppDbContext dbContext,
        IMapper mapper,
        IEmbeddingSyncService embeddingSyncService,
        IOptions<MovieEmbeddingRetryOptions> retryOptions,
        TimeProvider timeProvider)
    {
        _db = dbContext;
        _mapper = mapper;
        _embeddingSyncService = embeddingSyncService;
        _retryOptions = retryOptions.Value;
        _timeProvider = timeProvider;
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
        var result = _mapper.Map<List<MovieDto>>(movies);
        await ApplyEmbeddingStatusesAsync(result, cancellationToken);
        return result;
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

        if (movie is null) return null;

        var result = _mapper.Map<MovieDto>(movie);
        await ApplyEmbeddingStatusesAsync([result], cancellationToken);
        return result;
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

        if (movie.Status != "Inactive")
        {
            AddPendingState(movie.Id);
        }

        await _db.SaveChangesAsync(cancellationToken);
        var syncResult = await _embeddingSyncService.SyncMovieEmbeddingAsync(
            movie.Id,
            cancellationToken: cancellationToken);
        var created = (await GetByIdAsync(movie.Id, cancellationToken))!;
        created.EmbeddingStatus = syncResult.Status;
        return created;
    }

    public async Task<MovieDto?> UpdateWithEmbeddingAsync(
        Guid id,
        MovieDto dto,
        CancellationToken cancellationToken = default)
    {
        var movie = await _db.Movies
            .Include(m => m.MovieGenres)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        if (movie is null) return null;

        var previousTitle = movie.Title;
        var previousDescription = movie.Description;
        var previousDuration = movie.Duration;
        var previousLanguage = movie.Language;
        var previousRating = movie.Rating;
        var previousStatus = movie.Status;
        var previousGenreIds = movie.MovieGenres.Select(item => item.GenreId).ToHashSet();
        var effectiveGenreIds = previousGenreIds;

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
                    _db.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = gid });
                }

                effectiveGenreIds = validGenreIds.ToHashSet();
            }
            else
            {
                effectiveGenreIds = [];
            }
        }

        var semanticContentChanged = previousTitle != movie.Title
            || previousDescription != movie.Description
            || previousDuration != movie.Duration
            || previousLanguage != movie.Language
            || previousRating != movie.Rating
            || !previousGenreIds.SetEquals(effectiveGenreIds);
        if (movie.Status != "Inactive" && (semanticContentChanged || previousStatus == "Inactive"))
        {
            await MarkPendingStateAsync(movie.Id, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
        var syncResult = await _embeddingSyncService.SyncMovieEmbeddingAsync(
            movie.Id,
            cancellationToken: cancellationToken);
        var updated = await GetByIdAsync(movie.Id, cancellationToken);
        if (updated is not null)
        {
            updated.EmbeddingStatus = syncResult.Status;
        }

        return updated;
    }

    public async Task<bool> UpdateAsync(Guid id, MovieDto dto, CancellationToken cancellationToken = default) =>
        await UpdateWithEmbeddingAsync(id, dto, cancellationToken) is not null;

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var movie = await _db.Movies.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (movie is null) return false;

        var embedding = await _db.MovieEmbeddings
            .FirstOrDefaultAsync(item => item.MovieId == id, cancellationToken);
        if (embedding is not null)
        {
            _db.MovieEmbeddings.Remove(embedding);
        }

        var syncState = await _db.MovieEmbeddingSyncStates
            .FirstOrDefaultAsync(item => item.MovieId == id, cancellationToken);
        if (syncState is not null)
        {
            _db.MovieEmbeddingSyncStates.Remove(syncState);
        }

        _db.Movies.Remove(movie);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ApplyEmbeddingStatusesAsync(
        IReadOnlyCollection<MovieDto> movies,
        CancellationToken cancellationToken)
    {
        if (movies.Count == 0) return;

        var movieIds = movies.Select(movie => movie.Id).ToList();
        var stateByMovieId = await _db.MovieEmbeddingSyncStates
            .AsNoTracking()
            .Where(state => movieIds.Contains(state.MovieId))
            .ToDictionaryAsync(state => state.MovieId, state => state.Status, cancellationToken);
        var readyMovieIds = await _db.MovieEmbeddings
            .AsNoTracking()
            .Where(embedding => movieIds.Contains(embedding.MovieId) && embedding.Embedding.Length > 0)
            .Select(embedding => embedding.MovieId)
            .ToHashSetAsync(cancellationToken);

        foreach (var movie in movies)
        {
            if (movie.Status == "Inactive")
            {
                movie.EmbeddingStatus = MovieEmbeddingSyncStatuses.Ready;
            }
            else if (stateByMovieId.TryGetValue(movie.Id, out var status))
            {
                movie.EmbeddingStatus = status;
            }
            else
            {
                movie.EmbeddingStatus = readyMovieIds.Contains(movie.Id)
                    ? MovieEmbeddingSyncStatuses.Ready
                    : MovieEmbeddingSyncStatuses.Pending;
            }
        }
    }

    private void AddPendingState(Guid movieId)
    {
        var now = _timeProvider.GetUtcNow();
        _db.MovieEmbeddingSyncStates.Add(new MovieEmbeddingSyncState
        {
            MovieId = movieId,
            Status = MovieEmbeddingSyncStatuses.Pending,
            AttemptCount = 0,
            NextAttemptAt = now.AddSeconds(Math.Max(1, _retryOptions.BaseDelaySeconds))
        });
    }

    private async Task MarkPendingStateAsync(Guid movieId, CancellationToken cancellationToken)
    {
        var syncState = await _db.MovieEmbeddingSyncStates
            .FirstOrDefaultAsync(item => item.MovieId == movieId, cancellationToken);
        if (syncState is null)
        {
            AddPendingState(movieId);
            return;
        }

        syncState.Status = MovieEmbeddingSyncStatuses.Pending;
        syncState.AttemptCount = 0;
        syncState.NextAttemptAt = _timeProvider.GetUtcNow()
            .AddSeconds(Math.Max(1, _retryOptions.BaseDelaySeconds));
        syncState.LastError = string.Empty;
    }
}
