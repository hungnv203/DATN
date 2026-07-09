using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Infrastructure.Security;

namespace MovieBooking.Controllers;

[ApiController]
[Route("api")]
public class MovieReviewsController : ControllerBase
{
    private readonly IMovieReviewService _movieReviewService;

    public MovieReviewsController(IMovieReviewService movieReviewService)
    {
        _movieReviewService = movieReviewService;
    }

    [HttpGet("reviews")]
    [Authorize]
    [HasPermission("Read")]
    public async Task<ActionResult<IReadOnlyList<MovieReviewDto>>> GetAllReviews(
        CancellationToken cancellationToken)
    {
        var reviews = await _movieReviewService.GetAllReviewsAsync(cancellationToken);
        return Ok(reviews);
    }

    [HttpGet("movies/{movieId:guid}/reviews")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<MovieReviewDto>>> GetReviews(
        Guid movieId,
        CancellationToken cancellationToken)
    {
        var reviews = await _movieReviewService.GetVisibleReviewsAsync(movieId, cancellationToken);
        return Ok(reviews);
    }

    [HttpGet("movies/{movieId:guid}/rating-summary")]
    [AllowAnonymous]
    public async Task<ActionResult<MovieRatingSummaryDto>> GetRatingSummary(
        Guid movieId,
        CancellationToken cancellationToken)
    {
        var summary = await _movieReviewService.GetRatingSummaryAsync(movieId, cancellationToken);
        return Ok(summary);
    }

    [HttpPost("movies/{movieId:guid}/reviews")]
    [Authorize]
    public async Task<ActionResult<MovieReviewDto>> CreateReview(
        Guid movieId,
        [FromBody] CreateMovieReviewDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var review = await _movieReviewService.CreateReviewAsync(movieId, dto, cancellationToken);
            return CreatedAtAction(nameof(GetReviews), new { movieId }, review);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("reviews/{reviewId:guid}/hide")]
    [Authorize]
    [HasPermission("Update")]
    public async Task<IActionResult> HideReview(Guid reviewId, CancellationToken cancellationToken)
    {
        var updated = await _movieReviewService.HideReviewAsync(reviewId, cancellationToken);
        return updated ? NoContent() : NotFound();
    }
}
