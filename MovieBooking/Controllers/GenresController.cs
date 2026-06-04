using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[Route("api/genres")]
public class GenresController : CrudController<Genre, GenreDto>
{
    public GenresController(ICrudService<Genre, GenreDto> crudService) : base(crudService) { }
}
