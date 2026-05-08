namespace MovieBooking.Application.Common.DTOs;

public class MovieDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Duration { get; set; }
    public DateTime ReleaseDate { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Rating { get; set; } = string.Empty;
    public string PosterUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
