using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Constants;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

internal sealed class DashboardService : IDashboardService
{
    private readonly AppDbContext _dbContext;

    public DashboardService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(int days = 30, CancellationToken cancellationToken = default)
    {
        if (days <= 0) days = 30;

        var nowUtc = DateTime.UtcNow;
        var startDateTime = nowUtc.Date.AddDays(-days);
        var endDateTime = nowUtc.Date.AddDays(1);
        var startDateOffset = new DateTimeOffset(startDateTime, TimeSpan.Zero);
        var endDateOffset = new DateTimeOffset(endDateTime, TimeSpan.Zero);

        var paidBookingsQuery = _dbContext.Bookings
            .AsNoTracking()
            .Where(b => b.Status == BookingStatuses.Paid);

        var allBookingsInPeriodQuery = _dbContext.Bookings
            .AsNoTracking()
            .Where(b => b.CreatedAt >= startDateOffset && b.CreatedAt < endDateOffset);

        var totalRevenue = await paidBookingsQuery
            .Where(b => b.CreatedAt >= startDateOffset && b.CreatedAt < endDateOffset)
            .SumAsync(b => b.TotalPrice, cancellationToken);

        var totalTickets = await paidBookingsQuery
            .Where(b => b.CreatedAt >= startDateOffset && b.CreatedAt < endDateOffset)
            .SelectMany(b => b.Tickets)
            .CountAsync(cancellationToken);

        var totalUsers = await _dbContext.Users
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var activeMovies = await _dbContext.Movies
            .AsNoTracking()
            .CountAsync(m => m.Status == "NowShowing" || m.Status == "Active", cancellationToken);
        if (activeMovies == 0)
        {
            activeMovies = await _dbContext.Movies.AsNoTracking().CountAsync(cancellationToken);
        }

        var dailyRawPoints = await paidBookingsQuery
            .Where(b => b.CreatedAt >= startDateOffset && b.CreatedAt < endDateOffset)
            .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month, b.CreatedAt.Day })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                Revenue = g.Sum(b => b.TotalPrice)
            })
            .OrderBy(p => p.Year).ThenBy(p => p.Month).ThenBy(p => p.Day)
            .ToListAsync(cancellationToken);

        var dailyRevenue = dailyRawPoints.Select(p => new DailyRevenuePointDto
        {
            Date = new DateTime(p.Year, p.Month, p.Day, 0, 0, 0, DateTimeKind.Utc),
            Revenue = p.Revenue
        }).ToList();

        var statusDistribution = await allBookingsInPeriodQuery
            .GroupBy(b => b.Status)
            .Select(g => new BookingStatusDistributionDto
            {
                Status = g.Key,
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var totalBookingsInPeriod = statusDistribution.Sum(s => s.Count);
        foreach (var item in statusDistribution)
        {
            item.Percentage = totalBookingsInPeriod > 0
                ? Math.Round((double)item.Count / totalBookingsInPeriod * 100, 1)
                : 0;
        }

        var topMovies = await paidBookingsQuery
            .Where(b => b.CreatedAt >= startDateOffset && b.CreatedAt < endDateOffset)
            .SelectMany(b => b.Tickets)
            .GroupBy(t => new { t.Booking.Showtime.MovieId, t.Booking.Showtime.Movie.Title })
            .Select(g => new TopMovieDto
            {
                MovieId = g.Key.MovieId,
                Title = g.Key.Title,
                TicketsSold = g.Count(),
                Revenue = g.Sum(t => t.Price)
            })
            .OrderByDescending(m => m.Revenue)
            .Take(5)
            .ToListAsync(cancellationToken);

        var userGrowth = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.CreatedAt >= startDateOffset)
            .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
            .Select(g => new UserGrowthDto
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                NewUsers = g.Count()
            })
            .OrderBy(g => g.Year).ThenBy(g => g.Month)
            .ToListAsync(cancellationToken);

        var cinemas = await _dbContext.Cinemas
            .AsNoTracking()
            .Select(c => new
            {
                c.Id,
                c.Name
            })
            .ToListAsync(cancellationToken);

        var showtimesInPeriod = await _dbContext.Showtimes
            .AsNoTracking()
            .Where(s => s.StartTime >= startDateTime && s.StartTime < endDateTime)
            .Select(s => new
            {
                CinemaId = s.Room.CinemaId,
                RoomSeatCount = s.Room.Seats.Count(),
                TicketsSold = s.Bookings
                    .Where(b => b.Status == BookingStatuses.Paid)
                    .SelectMany(b => b.Tickets)
                    .Count()
            })
            .ToListAsync(cancellationToken);

        var cinemaPerformance = cinemas.Select(cinema =>
        {
            var cinemaShowtimes = showtimesInPeriod.Where(s => s.CinemaId == cinema.Id).ToList();
            var totalTicketsSold = cinemaShowtimes.Sum(s => s.TicketsSold);
            var totalAvailableSeats = cinemaShowtimes.Sum(s => s.RoomSeatCount);

            double occupancyRate = 0;
            if (totalAvailableSeats > 0)
            {
                occupancyRate = Math.Round((double)totalTicketsSold / totalAvailableSeats * 100, 1);
            }

            return new CinemaPerformanceDto
            {
                CinemaId = cinema.Id,
                CinemaName = cinema.Name,
                OccupancyRate = Math.Min(100.0, Math.Max(0.0, occupancyRate))
            };
        }).ToList();

        return new DashboardSummaryDto
        {
            TotalRevenue = totalRevenue,
            TotalTickets = totalTickets,
            TotalUsers = totalUsers,
            ActiveMovies = activeMovies,
            DailyRevenue = dailyRevenue,
            BookingStatusDistribution = statusDistribution,
            TopMovies = topMovies,
            UserGrowth = userGrowth,
            CinemaPerformance = cinemaPerformance
        };
    }
}
