namespace MovieBooking.Application.Common.DTOs;

public class MovieReviewDto
{
    public Guid Id { get; set; }
    public Guid MovieId { get; set; }
    public Guid UserId { get; set; }
    public Guid? BookingId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
