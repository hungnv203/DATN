using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class MovieGenre : BaseEntity
{
    public Guid MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public Guid GenreId { get; set; }
    public Genre Genre { get; set; } = null!;
}
