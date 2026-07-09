using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/bookings")]
public class BookingsController : CrudController<Booking, BookingDto>
{
    private readonly IBookingService _bookingService;
    private readonly IPricingService _pricingService;

    public BookingsController(IBookingService bookingService, IPricingService pricingService) : base(bookingService)
    {
        _bookingService = bookingService;
        _pricingService = pricingService;
    }

    [HttpPost]
    [AllowAnonymous]
    public override async Task<ActionResult<BookingDto>> Create([FromBody] BookingDto dto, CancellationToken cancellationToken)
    {
        try
        {
            return await base.Create(dto, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("hold-seats")]
    public async Task<ActionResult<SeatHoldResultDto>> HoldSeats([FromBody] HoldSeatsRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _bookingService.HoldSeatsAsync(request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new SeatHoldResultDto { Success = false, Message = ex.Message });
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
}
