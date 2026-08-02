using MovieBooking.Application.Common.DTOs;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Application.Common.Interfaces;

public interface ITicketService : ICrudService<Ticket, TicketDto>
{
}

