using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<SeatHold> SeatHolds { get; set; } = new List<SeatHold>();
    public LoyaltyPoint? LoyaltyPoint { get; set; }
    public ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
