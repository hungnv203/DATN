using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class MovieStatusUpdateService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MovieStatusUpdateService> _logger;

    public MovieStatusUpdateService(
        IServiceScopeFactory scopeFactory,
        ILogger<MovieStatusUpdateService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Movie Status Update Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var today = DateTime.UtcNow.Date;
                var moviesToStart = await dbContext.Movies
                    .Where(movie => movie.Status == "Upcoming"
                                    && movie.ReleaseDate.Date <= today)
                    .ToListAsync(stoppingToken);

                if (moviesToStart.Count > 0)
                {
                    _logger.LogInformation(
                        "Found {Count} movies ready to show. Marking as NowShowing...",
                        moviesToStart.Count);

                    foreach (var movie in moviesToStart)
                    {
                        movie.Status = "NowShowing";
                        movie.MarkUpdated(DateTimeOffset.UtcNow);
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during movie status update.");
            }

            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }

        _logger.LogInformation("Movie Status Update Service is stopping.");
    }
}
