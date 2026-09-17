using MovieBooking.Application.Common.DTOs;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Application.Common.Interfaces;

public interface ITicketService : ICrudService<Ticket, TicketDto>
{
    Task<(bool Success, string Message, Guid? TicketId)> CheckInAsync(string qrCode, CancellationToken cancellationToken = default);
}

