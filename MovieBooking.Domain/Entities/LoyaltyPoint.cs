using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class LoyaltyPoint : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int Points { get; set; }
}
