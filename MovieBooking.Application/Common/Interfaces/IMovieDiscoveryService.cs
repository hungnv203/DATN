using MovieBooking.Application.Common.DTOs;

namespace MovieBooking.Application.Common.Interfaces;

public interface IMovieDiscoveryService
{
    Task<MovieDiscoveryDto> GetDiscoveryAsync(
        int limit = 10,
        CancellationToken cancellationToken = default);
}
