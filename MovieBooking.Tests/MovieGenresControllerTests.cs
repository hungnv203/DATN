using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Controllers;
using Xunit;

namespace MovieBooking.Tests;

public sealed class MovieGenresControllerTests
{
    [Fact]
    public async Task MutationEndpoints_ReturnMethodNotAllowed()
    {
        var controller = new MovieGenresController(new StubMovieGenreService());

        var create = await controller.Create(new MovieGenreDto(), CancellationToken.None);
        var update = await controller.Update(Guid.NewGuid(), new MovieGenreDto(), CancellationToken.None);
        var delete = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(
            StatusCodes.Status405MethodNotAllowed,
            Assert.IsType<ObjectResult>(create.Result).StatusCode);
        Assert.Equal(
            StatusCodes.Status405MethodNotAllowed,
            Assert.IsType<ObjectResult>(update).StatusCode);
        Assert.Equal(
            StatusCodes.Status405MethodNotAllowed,
            Assert.IsType<ObjectResult>(delete).StatusCode);
    }

    private sealed class StubMovieGenreService : IMovieGenreService
    {
        public Task<IReadOnlyList<MovieGenreDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MovieGenreDto>>([]);

        public Task<MovieGenreDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<MovieGenreDto?>(null);

        public Task<MovieGenreDto> CreateAsync(MovieGenreDto dto, CancellationToken cancellationToken = default) =>
            Task.FromResult(dto);

        public Task<bool> UpdateAsync(Guid id, MovieGenreDto dto, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
