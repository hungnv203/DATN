using AutoMapper;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

internal sealed class MovieGenreService : IMovieGenreService
{
    private readonly EntityCrudOperations<MovieGenre, MovieGenreDto> _operations;

    public MovieGenreService(AppDbContext dbContext, IMapper mapper)
    {
        _operations = new EntityCrudOperations<MovieGenre, MovieGenreDto>(dbContext, mapper);
    }

    public Task<IReadOnlyList<MovieGenreDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _operations.GetAllAsync(cancellationToken);

    public Task<MovieGenreDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.GetByIdAsync(id, cancellationToken);

    public Task<MovieGenreDto> CreateAsync(MovieGenreDto dto, CancellationToken cancellationToken = default) =>
        _operations.CreateAsync(dto, cancellationToken);

    public Task<bool> UpdateAsync(Guid id, MovieGenreDto dto, CancellationToken cancellationToken = default) =>
        _operations.UpdateAsync(id, dto, cancellationToken);

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.DeleteAsync(id, cancellationToken);
}

