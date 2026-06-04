using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/movie-genres")]
public class MovieGenresController : CrudController<MovieGenre, MovieGenreDto>
{
    public MovieGenresController(ICrudService<MovieGenre, MovieGenreDto> crudService) : base(crudService) { }
}
