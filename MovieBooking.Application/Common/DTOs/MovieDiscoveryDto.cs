namespace MovieBooking.Application.Common.DTOs;

public class MovieDiscoveryDto
{
    public IReadOnlyList<MovieDto> Featured { get; set; } = [];
    public IReadOnlyList<MovieDto> Trending { get; set; } = [];
    public IReadOnlyList<MovieDto> TopRated { get; set; } = [];
    public IReadOnlyList<MovieDto> BestSelling { get; set; } = [];
    public IReadOnlyList<MovieDto> NewReleases { get; set; } = [];
    public IReadOnlyList<MovieDto> Upcoming { get; set; } = [];
}
