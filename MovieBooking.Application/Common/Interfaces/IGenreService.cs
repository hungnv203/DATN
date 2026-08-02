using MovieBooking.Application.Common.DTOs;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Application.Common.Interfaces;

public interface IGenreService : ICrudService<Genre, GenreDto>
{
}

