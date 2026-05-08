namespace MovieBooking.Application.Common.DTOs;

public class RoomDto
{
    public Guid Id { get; set; }
    public Guid CinemaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public string Type { get; set; } = string.Empty;
}
