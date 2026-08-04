using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MovieBooking.Application.Common.Interfaces;

namespace MovieBooking.Infrastructure.Services;

public sealed class ExpiredSeatHoldsCleanupService : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(30);

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
        _logger.LogInformation("Expired seat-hold cleanup service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var seatHoldService = scope.ServiceProvider.GetRequiredService<ISeatHoldService>();
                var publisher = scope.ServiceProvider.GetRequiredService<ISeatRealtimePublisher>();
                var batches = await seatHoldService.ExpireElapsedAsync(stoppingToken);
                foreach (var batch in batches)
                {
                    await publisher.PublishAsync(batch, stoppingToken);
                }
                var expiredCount = batches.Sum(batch => batch.Changes.Count);
                if (expiredCount > 0)
                {
                    _logger.LogInformation(
                        "Seat-hold cleanup transitioned {Count} rows to Expired.",
                        expiredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Seat-hold cleanup failed.");
            }

            await Task.Delay(CleanupInterval, stoppingToken);
        }
    }
}
