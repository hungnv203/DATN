using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieBooking.Application.Common.Configuration;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Constants;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services.Assistant;

public sealed class MovieEmbeddingRetryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MovieEmbeddingRetryService> _logger;
    private readonly MovieEmbeddingRetryOptions _options;
    private readonly TimeProvider _timeProvider;

    public MovieEmbeddingRetryService(
        IServiceScopeFactory scopeFactory,
        ILogger<MovieEmbeddingRetryService> logger,
        IOptions<MovieEmbeddingRetryOptions> options,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Movie embedding retry service is starting.");
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueRetriesAsync(stoppingToken);
                await Task.Delay(pollInterval, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Movie embedding retry batch failed.");
                try
                {
                    await Task.Delay(pollInterval, _timeProvider, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    internal async Task<int> ProcessDueRetriesAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var syncService = scope.ServiceProvider.GetRequiredService<IEmbeddingSyncService>();
        var now = _timeProvider.GetUtcNow();
        var batchSize = Math.Max(1, _options.BatchSize);

        var movieIds = await db.MovieEmbeddingSyncStates
            .AsNoTracking()
            .Where(state => state.Status == MovieEmbeddingSyncStatuses.Pending
                && state.NextAttemptAt != null
                && state.NextAttemptAt <= now)
            .OrderBy(state => state.NextAttemptAt)
            .Take(batchSize)
            .Select(state => state.MovieId)
            .ToListAsync(cancellationToken);

        foreach (var movieId in movieIds)
        {
            var result = await syncService.SyncMovieEmbeddingAsync(
                movieId,
                isRetryAttempt: true,
                cancellationToken);
            _logger.LogInformation(
                "Processed embedding retry for movie {MovieId} with result {Status}.",
                movieId,
                result.Status);
        }

        return movieIds.Count;
    }
}
