using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/movies")]
public class MoviesController : CrudController<Movie, MovieDto>
{
    public MoviesController(ICrudService<Movie, MovieDto> crudService) : base(crudService) { }
}
