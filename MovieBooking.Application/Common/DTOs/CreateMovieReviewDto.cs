namespace MovieBooking.Application.Common.DTOs;

public class CreateMovieReviewDto
{
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
}
