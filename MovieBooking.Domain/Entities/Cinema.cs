using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class Cinema : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}
