using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/cinemas")]
public class CinemasController : CrudController<Cinema, CinemaDto>
{
    public CinemasController(ICrudService<Cinema, CinemaDto> crudService) : base(crudService) { }
}
