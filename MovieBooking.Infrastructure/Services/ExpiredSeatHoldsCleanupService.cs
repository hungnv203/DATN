using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class ExpiredSeatHoldsCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredSeatHoldsCleanupService> _logger;

    public ExpiredSeatHoldsCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredSeatHoldsCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Expired Seat Holds Cleanup Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var now = DateTime.UtcNow;
                    var expiredHolds = await dbContext.SeatHolds
                        .Where(sh => sh.ExpiredAt <= now)
                        .ToListAsync(stoppingToken);

                    if (expiredHolds.Any())
                    {
                        _logger.LogInformation("Found {Count} expired seat holds. Deleting...", expiredHolds.Count);
                        dbContext.SeatHolds.RemoveRange(expiredHolds);
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during expired seat holds cleanup.");
            }

            // Run every 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("Expired Seat Holds Cleanup Service is stopping.");
    }
}
