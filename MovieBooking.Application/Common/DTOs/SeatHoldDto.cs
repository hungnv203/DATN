namespace MovieBooking.Application.Common.DTOs;

public class SeatHoldDto
{
    public Guid Id { get; set; }
    public Guid ShowtimeId { get; set; }
    public Guid SeatId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ExpiredAt { get; set; }
}
