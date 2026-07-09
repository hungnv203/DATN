using MovieBooking.Domain.Common;

namespace MovieBooking.Domain.Entities;

public class Movie : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Duration { get; set; }
    public DateTime ReleaseDate { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Rating { get; set; } = string.Empty;
    public string PosterUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
    public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
    public ICollection<MovieReview> Reviews { get; set; } = new List<MovieReview>();
}
