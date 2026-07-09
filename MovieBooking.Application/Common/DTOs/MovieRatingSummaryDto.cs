namespace MovieBooking.Application.Common.DTOs;

public class MovieRatingSummaryDto
{
    public Guid MovieId { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public Dictionary<int, int> RatingBreakdown { get; set; } = new();
}
