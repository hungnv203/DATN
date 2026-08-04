using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Exceptions;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Security;

namespace MovieBooking.Controllers;

[Route("api/bookings")]
public class BookingsController : CrudController<Booking, BookingDto>
{
    private readonly IBookingService _bookingService;
    private readonly IPricingService _pricingService;
    private readonly ISeatHoldService _seatHoldService;
    private readonly ISeatRealtimePublisher _seatRealtimePublisher;
    private readonly IPaymentWorkflowService _paymentWorkflowService;

    public BookingsController(
        IBookingService bookingService,
        IPricingService pricingService,
        ISeatHoldService seatHoldService,
        ISeatRealtimePublisher seatRealtimePublisher,
        IPaymentWorkflowService paymentWorkflowService) : base(bookingService)
    {
        _bookingService = bookingService;
        _pricingService = pricingService;
        _seatHoldService = seatHoldService;
        _seatRealtimePublisher = seatRealtimePublisher;
        _paymentWorkflowService = paymentWorkflowService;
    }

    [HttpPost]
    [SkipPermission]
    public override async Task<ActionResult<BookingDto>> Create([FromBody] BookingDto dto, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _bookingService.CreateAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (SeatHoldConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception)
        {
            return Conflict(new
            {
                message = "The booking could not be created because availability changed."
            });
        }
    }

    [HttpPost("pos")]
    [MovieBooking.Infrastructure.Security.HasPermission("Create")]
    public async Task<ActionResult<BookingDto>> CreatePointOfSale(
        [FromBody] CreatePosBookingRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _bookingService.CreatePointOfSaleAsync(new BookingDto
            {
                ShowtimeId = request.ShowtimeId,
                SeatIds = request.SeatIds.ToList(),
                SeatHoldGroupId = request.SeatHoldGroupId
            }, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception)
        {
            return Conflict(new
            {
                message = "The booking could not be created because availability changed."
            });
        }
    }

    [HttpPost("{bookingId:guid}/pos-payment-confirmations")]
    [HasPermission("Create")]
    public async Task<ActionResult<PaymentTransitionResultDto>> ConfirmPointOfSalePayment(
        Guid bookingId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] PosPaymentConfirmationRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(idempotencyKey, out var key)
            || request.Method != MovieBooking.Domain.Constants.PaymentMethods.Cash)
        {
            return BadRequest(new { message = "A UUID Idempotency-Key and Cash method are required." });
        }

        var result = await _paymentWorkflowService.ConfirmPosCashAsync(
            GetCurrentUserId(),
            bookingId,
            key,
            cancellationToken);
        if (!result.Success)
        {
            return ToWorkflowFailure(result);
        }

        if (result.ChangeBatch != null)
        {
            await _seatRealtimePublisher.PublishAsync(result.ChangeBatch, cancellationToken);
        }

        return result.IsReplay ? Ok(result) : StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{bookingId:guid}/pos-cancellations")]
    [HasPermission("Create")]
    public async Task<ActionResult<PaymentTransitionResultDto>> CancelPointOfSale(
        Guid bookingId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] PosCancellationRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(idempotencyKey, out var key))
        {
            return BadRequest(new { message = "A UUID Idempotency-Key is required." });
        }

        var result = await _paymentWorkflowService.CancelPosAsync(
            GetCurrentUserId(),
            bookingId,
            key,
            request.ReasonCode,
            cancellationToken);
        if (!result.Success)
        {
            return ToWorkflowFailure(result);
        }

        if (result.ChangeBatch != null)
        {
            await _seatRealtimePublisher.PublishAsync(result.ChangeBatch, cancellationToken);
        }

        return Ok(result);
    }

    public override Task<IActionResult> Update(
        Guid id,
        BookingDto dto,
        CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status405MethodNotAllowed));

    public override Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status405MethodNotAllowed));

    [HttpPost("hold-seats")]
    [Authorize]
    public async Task<ActionResult<SeatHoldResultDto>> HoldSeats([FromBody] HoldSeatsRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _seatHoldService.CreateOrReplaceForShowtimeAsync(
                GetCurrentUserId(),
                request,
                cancellationToken);
            if (result.ChangeBatch != null)
            {
                await _seatRealtimePublisher.PublishAsync(result.ChangeBatch, cancellationToken);
            }
            if (!result.Success)
            {
                return result.ErrorCode switch
                {
                    "SHOWTIME_NOT_FOUND" => NotFound(result),
                    "SEAT_NOT_AVAILABLE" => Conflict(result),
                    _ => BadRequest(result)
                };
            }
            return Ok(result);
        }
        catch (Exception)
        {
            return Conflict(new SeatHoldResultDto
            {
                Success = false,
                Message = "The selected seats are no longer available."
            });
        }
    }

    [HttpPost("quote")]
    public async Task<ActionResult<BookingQuoteDto>> Quote([FromBody] BookingQuoteRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var quote = await _pricingService.QuoteAsync(request, userId == Guid.Empty ? null : userId, cancellationToken);
            return Ok(quote);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("my-tickets")]
    public async Task<ActionResult<List<MyTicketDto>>> GetMyTickets(CancellationToken cancellationToken)
    {
        var tickets = await _bookingService.GetMyTicketsAsync(cancellationToken);
        return Ok(tickets);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? User.FindFirst("sub");
        return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId) ? userId : Guid.Empty;
    }

    private ActionResult<PaymentTransitionResultDto> ToWorkflowFailure(
        PaymentTransitionResultDto result)
    {
        return result.ErrorCode.EndsWith("NOT_FOUND", StringComparison.Ordinal)
            ? NotFound(result)
            : Conflict(result);
    }
}
