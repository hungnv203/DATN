namespace MovieBooking.Application.Common.DTOs;

public class SeatDto
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string Type { get; set; } = string.Empty;
}
