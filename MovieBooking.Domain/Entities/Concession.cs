using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class Concession : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<BookingConcession> BookingConcessions { get; set; } = new List<BookingConcession>();
}
