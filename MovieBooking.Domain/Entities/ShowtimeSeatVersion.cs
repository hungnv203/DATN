namespace MovieBooking.Domain.Entities;

public sealed class ShowtimeSeatVersion
{
    public Guid ShowtimeId { get; set; }
    public long Version { get; set; }
    public Showtime Showtime { get; set; } = null!;
}
