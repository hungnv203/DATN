using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/tickets")]
public class TicketsController : CrudController<Ticket, TicketDto>
{
    public TicketsController(ICrudService<Ticket, TicketDto> crudService) : base(crudService) { }
}
