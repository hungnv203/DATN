using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class MovieReviewService : IMovieReviewService
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MovieReviewService(AppDbContext db, IMapper mapper, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _mapper = mapper;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlyList<MovieReviewDto>> GetAllReviewsAsync(
        CancellationToken cancellationToken = default)
    {
        var reviews = await _db.MovieReviews
            .AsNoTracking()
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return reviews.Select(r => _mapper.Map<MovieReviewDto>(r)).ToList();
    }

    public async Task<IReadOnlyList<MovieReviewDto>> GetVisibleReviewsAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var reviews = await _db.MovieReviews
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.MovieId == movieId && r.Status == "Visible")
            .OrderByDescending(r => r.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return reviews.Select(r => _mapper.Map<MovieReviewDto>(r)).ToList();
    }

    public async Task<MovieRatingSummaryDto> GetRatingSummaryAsync(
        Guid movieId,
        CancellationToken cancellationToken = default)
    {
        var ratings = await _db.MovieReviews
            .AsNoTracking()
            .Where(r => r.MovieId == movieId && r.Status == "Visible")
            .Select(r => r.Rating)
            .ToListAsync(cancellationToken);

        return new MovieRatingSummaryDto
        {
            MovieId = movieId,
            AverageRating = ratings.Count == 0 ? 0 : Math.Round(ratings.Average(), 1),
            TotalReviews = ratings.Count,
            RatingBreakdown = Enumerable.Range(1, 5).ToDictionary(
                rating => rating,
                rating => ratings.Count(value => value == rating))
        };
    }

    public async Task<MovieReviewDto> CreateReviewAsync(
        Guid movieId,
        CreateMovieReviewDto dto,
        CancellationToken cancellationToken = default)
    {
        if (dto.Rating < 1 || dto.Rating > 5)
        {
            throw new InvalidOperationException("Rating phải nằm trong khoảng 1 đến 5.");
        }

        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("Vui lòng đăng nhập để đánh giá phim.");
        }

        var alreadyReviewed = await _db.MovieReviews.AnyAsync(
            r => r.MovieId == movieId && r.UserId == userId,
            cancellationToken);
        if (alreadyReviewed)
        {
            throw new InvalidOperationException("Bạn đã đánh giá phim này.");
        }

        var eligibleBooking = await _db.Bookings
            .Include(b => b.Showtime)
            .Where(b => b.UserId == userId
                && b.Status == "Paid"
                && b.Showtime.MovieId == movieId
                && b.Showtime.StartTime <= DateTime.UtcNow)
            .OrderByDescending(b => b.Showtime.StartTime)
            .FirstOrDefaultAsync(cancellationToken);
        if (eligibleBooking == null)
        {
            throw new InvalidOperationException("Bạn chỉ có thể đánh giá phim đã mua vé và đã xem.");
        }

        var review = new MovieReview
        {
            MovieId = movieId,
            UserId = userId,
            BookingId = eligibleBooking.Id,
            Rating = dto.Rating,
            Comment = dto.Comment.Trim(),
            Status = "Visible"
        };

        _db.MovieReviews.Add(review);
        await _db.SaveChangesAsync(cancellationToken);

        await _db.Entry(review).Reference(r => r.User).LoadAsync(cancellationToken);
        return _mapper.Map<MovieReviewDto>(review);
    }

    public async Task<bool> HideReviewAsync(Guid reviewId, CancellationToken cancellationToken = default)
    {
        var review = await _db.MovieReviews.FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken);
        if (review == null)
        {
            return false;
        }

        review.Status = "Hidden";
        review.MarkUpdated(DateTimeOffset.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Guid GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var claim = user?.FindFirst(ClaimTypes.NameIdentifier) ?? user?.FindFirst("sub");
        return claim != null && Guid.TryParse(claim.Value, out var userId) ? userId : Guid.Empty;
    }
}
