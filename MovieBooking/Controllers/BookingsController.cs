using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/bookings")]
public class BookingsController : CrudController<Booking, BookingDto>
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService) : base(bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpPost]
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
}
