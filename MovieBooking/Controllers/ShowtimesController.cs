using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/showtimes")]
public class ShowtimesController : CrudController<Showtime, ShowtimeDto>
{
    public ShowtimesController(ICrudService<Showtime, ShowtimeDto> crudService) : base(crudService) { }
}
