using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class ExpiredBookingsCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredBookingsCleanupService> _logger;

    public ExpiredBookingsCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredBookingsCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Expired Bookings Cleanup Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var loyaltyService = scope.ServiceProvider.GetRequiredService<ILoyaltyService>();

                var now = DateTime.UtcNow;
                var expiredBookings = await dbContext.Bookings
                    .Include(b => b.Tickets)
                    .Include(b => b.Payment)
                    .Where(b => b.Status == "Pending"
                                && b.ExpiredAt != null
                                && b.ExpiredAt <= now)
                    .ToListAsync(stoppingToken);

                if (expiredBookings.Count > 0)
                {
                    _logger.LogInformation(
                        "Found {Count} expired bookings. Marking as expired...",
                        expiredBookings.Count);

                    foreach (var booking in expiredBookings)
                    {
                        booking.Status = "Expired";

                        foreach (var ticket in booking.Tickets)
                        {
                            ticket.Status = "Expired";
                        }

                        if (booking.Payment?.Status == "Pending")
                        {
                            booking.Payment.Status = "Expired";
                        }

                        await loyaltyService.ReturnRedeemedPointsAsync(booking.Id, stoppingToken);
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during expired bookings cleanup.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("Expired Bookings Cleanup Service is stopping.");
    }
}
