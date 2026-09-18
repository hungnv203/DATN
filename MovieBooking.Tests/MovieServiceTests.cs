using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Mapping;
using MovieBooking.Infrastructure.Persistence;
using MovieBooking.Infrastructure.Services;
using Xunit;

namespace MovieBooking.Tests;

public sealed class MovieServiceTests
{
    [Fact]
    public async Task UpdateAsync_AddsGenreToLegacyMovieWithoutExistingGenres()
    {
        await using var db = CreateDbContext();
        var genre = new Genre { Name = "Action" };
        var movie = new Movie
        {
            Title = "Legacy movie",
            Description = "Created before genre assignment was supported",
            Duration = 120,
            ReleaseDate = DateTime.UtcNow,
            Language = "Vietnamese",
            Rating = "T13",
            PosterUrl = "poster.jpg",
            Status = "NowShowing"
        };

        db.AddRange(genre, movie);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = new MovieService(db, CreateMapper());
        var dto = new MovieDto
        {
            Id = movie.Id,
            Title = movie.Title,
            Description = movie.Description,
            Duration = movie.Duration,
            ReleaseDate = movie.ReleaseDate,
            Language = movie.Language,
            Rating = movie.Rating,
            PosterUrl = movie.PosterUrl,
            Status = movie.Status,
            GenreIds = [genre.Id]
        };

        var updated = await service.UpdateAsync(movie.Id, dto);

        Assert.True(updated);
        var assignment = await db.MovieGenres.SingleAsync(mg => mg.MovieId == movie.Id);
        Assert.Equal(genre.Id, assignment.GenreId);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IMapper CreateMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(config => config.AddProfile<EntityDtoProfile>());
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }
}
