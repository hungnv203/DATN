using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/seats")]
public class SeatsController : CrudController<Seat, SeatDto>
{
    public SeatsController(ICrudService<Seat, SeatDto> crudService) : base(crudService) { }
}
