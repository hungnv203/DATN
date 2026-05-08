namespace MovieBooking.Application.Common.DTOs;

public class MovieGenreDto
{
    public Guid Id { get; set; }
    public Guid MovieId { get; set; }
    public Guid GenreId { get; set; }
}
