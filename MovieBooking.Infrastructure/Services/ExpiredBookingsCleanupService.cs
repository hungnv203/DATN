using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Constants;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class ExpiredBookingsCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredBookingsCleanupService> _logger;
    private readonly TimeProvider _timeProvider;

    public ExpiredBookingsCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredBookingsCleanupService> logger,
        TimeProvider timeProvider)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _timeProvider = timeProvider;
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
                var publisher = scope.ServiceProvider.GetRequiredService<ISeatRealtimePublisher>();

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var expiredBookings = await dbContext.Bookings
                    .Include(b => b.Tickets)
                    .Include(b => b.Payment)
                    .Where(b => b.Status == "Pending"
                                && b.ExpiredAt != null
                                && b.ExpiredAt <= now)
                    .ToListAsync(stoppingToken);

                if (expiredBookings.Count > 0)
                {
                    await using var transaction = await dbContext.Database.BeginTransactionAsync(stoppingToken);
                    _logger.LogInformation(
                        "Found {Count} expired bookings. Marking as expired...",
                        expiredBookings.Count);

                    var holdGroupIds = expiredBookings
                        .Where(booking => booking.SeatHoldGroupId.HasValue)
                        .Select(booking => booking.SeatHoldGroupId!.Value)
                        .ToArray();
                    var linkedHolds = await dbContext.SeatHolds
                        .Where(hold => holdGroupIds.Contains(hold.HoldGroupId)
                                       && hold.Status == SeatHoldStatuses.Active)
                        .ToListAsync(stoppingToken);

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

                    foreach (var hold in linkedHolds)
                    {
                        hold.Status = SeatHoldStatuses.Expired;
                        hold.MarkUpdated(new DateTimeOffset(now, TimeSpan.Zero));
                    }

                    var batches = new List<MovieBooking.Application.Common.DTOs.SeatStateChangeBatchDto>();
                    foreach (var group in linkedHolds.GroupBy(hold => hold.ShowtimeId))
                    {
                        var version = await ShowtimeSeatVersionStore.IncrementAsync(
                            dbContext, group.Key, stoppingToken);

                        batches.Add(new MovieBooking.Application.Common.DTOs.SeatStateChangeBatchDto
                        {
                            ShowtimeId = group.Key,
                            Version = version,
                            CommittedAtUtc = now,
                            Changes = group.Select(hold => new MovieBooking.Application.Common.DTOs.SeatStateChangeDto
                            {
                                SeatId = hold.SeatId,
                                Status = "Available",
                                HoldGroupId = hold.HoldGroupId
                            }).ToArray()
                        });
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);
                    await transaction.CommitAsync(stoppingToken);
                    foreach (var batch in batches)
                    {
                        await publisher.PublishAsync(batch, stoppingToken);
                    }
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
