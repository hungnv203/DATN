using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class BookingConcession : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    public Guid ConcessionId { get; set; }
    public Concession Concession { get; set; } = null!;

    public int Quantity { get; set; }
    public decimal Price { get; set; } // Price at the time of booking
}
