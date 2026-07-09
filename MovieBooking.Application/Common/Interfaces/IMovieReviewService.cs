using MovieBooking.Application.Common.DTOs;

namespace MovieBooking.Application.Common.Interfaces;

public interface IMovieReviewService
{
    Task<IReadOnlyList<MovieReviewDto>> GetAllReviewsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MovieReviewDto>> GetVisibleReviewsAsync(Guid movieId, CancellationToken cancellationToken = default);
    Task<MovieRatingSummaryDto> GetRatingSummaryAsync(Guid movieId, CancellationToken cancellationToken = default);
    Task<MovieReviewDto> CreateReviewAsync(Guid movieId, CreateMovieReviewDto dto, CancellationToken cancellationToken = default);
    Task<bool> HideReviewAsync(Guid reviewId, CancellationToken cancellationToken = default);
}
