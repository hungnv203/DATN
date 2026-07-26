namespace MovieBooking.Application.Common.DTOs;

public class SeatHoldDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid ShowtimeId { get; set; }
    public Guid SeatId { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiredAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? BookingId { get; set; }
}
