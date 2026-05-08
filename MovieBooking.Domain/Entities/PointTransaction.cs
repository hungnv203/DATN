using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class PointTransaction : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int Points { get; set; }
    public string Type { get; set; } = string.Empty;
}
