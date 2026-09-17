namespace MovieBooking.Application.Common.DTOs;

public class DashboardSummaryDto
{
    public decimal TotalRevenue { get; set; }
    public int TotalTickets { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveMovies { get; set; }
    public List<DailyRevenuePointDto> DailyRevenue { get; set; } = new();
    public List<BookingStatusDistributionDto> BookingStatusDistribution { get; set; } = new();
    public List<TopMovieDto> TopMovies { get; set; } = new();
    public List<UserGrowthDto> UserGrowth { get; set; } = new();
    public List<CinemaPerformanceDto> CinemaPerformance { get; set; } = new();
}

public class DailyRevenuePointDto
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
}

public class BookingStatusDistributionDto
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percentage { get; set; }
}

public class TopMovieDto
{
    public Guid MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TicketsSold { get; set; }
    public decimal Revenue { get; set; }
}

public class CinemaPerformanceDto
{
    public Guid CinemaId { get; set; }
    public string CinemaName { get; set; } = string.Empty;
    public double OccupancyRate { get; set; }
}

public class UserGrowthDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int NewUsers { get; set; }
}
