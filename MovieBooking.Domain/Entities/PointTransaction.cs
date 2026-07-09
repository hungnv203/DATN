using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class PointTransaction : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int Points { get; set; }
    public string Type { get; set; } = string.Empty;
    public int BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
}
