using System;
using System.Threading;
using System.Threading.Tasks;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Application.Common.Interfaces;

public interface IBookingService : ICrudService<Booking, BookingDto>
{
    Task<SeatHoldResultDto> HoldSeatsAsync(HoldSeatsRequestDto request, CancellationToken cancellationToken = default);
    Task<List<MyTicketDto>> GetMyTicketsAsync(CancellationToken cancellationToken = default);
}
