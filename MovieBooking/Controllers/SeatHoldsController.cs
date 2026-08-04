using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;

namespace MovieBooking.Controllers;

[ApiController]
[Authorize]
[Route("api/seat-holds")]
public sealed class SeatHoldsController : ControllerBase
{
    private readonly ISeatHoldService _seatHoldService;
    private readonly ISeatRealtimePublisher _publisher;

    public SeatHoldsController(ISeatHoldService seatHoldService, ISeatRealtimePublisher publisher)
    {
        _seatHoldService = seatHoldService;
        _publisher = publisher;
    }

    [HttpPost]
    public async Task<ActionResult<SeatHoldResultDto>> Create(
        [FromBody] HoldSeatsRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _seatHoldService.CreateOrReplaceForShowtimeAsync(
            GetCurrentUserId(),
            request,
            cancellationToken);
        await PublishAsync(result.ChangeBatch, cancellationToken);
        return ToActionResult(result, StatusCodes.Status201Created);
    }

    [HttpGet("{holdGroupId:guid}")]
    public async Task<ActionResult<SeatHoldResultDto>> GetByGroupId(
        Guid holdGroupId,
        CancellationToken cancellationToken)
    {
        var result = await _seatHoldService.GetOwnedGroupAsync(
            GetCurrentUserId(),
            holdGroupId,
            cancellationToken);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPut("{holdGroupId:guid}")]
    public async Task<ActionResult<SeatHoldResultDto>> Replace(
        Guid holdGroupId,
        [FromBody] HoldSeatsRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _seatHoldService.ReplaceAsync(
            GetCurrentUserId(),
            holdGroupId,
            request,
            cancellationToken);
        await PublishAsync(result.ChangeBatch, cancellationToken);
        return ToActionResult(result, StatusCodes.Status200OK);
    }

    [HttpDelete("{holdGroupId:guid}")]
    public async Task<IActionResult> Release(
        Guid holdGroupId,
        CancellationToken cancellationToken)
    {
        var released = await _seatHoldService.ReleaseAsync(
            GetCurrentUserId(),
            holdGroupId,
            cancellationToken);
        if (released == null)
        {
            return NotFound();
        }

        await _publisher.PublishAsync(released, cancellationToken);
        return NoContent();
    }

    private Task PublishAsync(SeatStateChangeBatchDto? batch, CancellationToken cancellationToken) =>
        batch == null ? Task.CompletedTask : _publisher.PublishAsync(batch, cancellationToken);

    private ActionResult<SeatHoldResultDto> ToActionResult(SeatHoldResultDto result, int successStatus)
    {
        if (result.Success)
        {
            return StatusCode(successStatus, result);
        }

        return result.ErrorCode switch
        {
            "SHOWTIME_NOT_FOUND" or "HOLD_NOT_FOUND" => NotFound(result),
            "SEAT_NOT_AVAILABLE" => Conflict(result),
            _ => BadRequest(result)
        };
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        if (claim == null || !Guid.TryParse(claim.Value, out var userId))
        {
            throw new UnauthorizedAccessException("The authenticated user identifier is invalid.");
        }

        return userId;
    }
}
