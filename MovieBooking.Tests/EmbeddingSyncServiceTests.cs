using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieBooking.Application.Common.Configuration;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Constants;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Mapping;
using MovieBooking.Infrastructure.Persistence;
using MovieBooking.Infrastructure.Services;
using MovieBooking.Infrastructure.Services.Assistant;
using Xunit;

namespace MovieBooking.Tests;

public sealed class EmbeddingSyncServiceTests
{
    [Fact]
    public async Task SyncMovieEmbeddingAsync_CreatesDeterministicEmbedding_AndSkipsUnchangedHash()
    {
        await using var db = CreateDbContext();
        var movie = await SeedMovieAsync(db, "Live sync", "Tâm lí", "Kinh dị");
        var embeddingService = new RecordingEmbeddingService();
        var service = CreateSyncService(db, embeddingService);

        var first = await service.SyncMovieEmbeddingAsync(movie.Id);
        var second = await service.SyncMovieEmbeddingAsync(movie.Id);

        Assert.Equal(MovieEmbeddingSyncStatuses.Ready, first.Status);
        Assert.Equal(MovieEmbeddingSyncStatuses.Ready, second.Status);
        Assert.Equal(1, embeddingService.CallCount);

        var stored = await db.MovieEmbeddings.SingleAsync(item => item.MovieId == movie.Id);
        Assert.Contains("Genres: Kinh dị, Tâm lí", stored.EmbeddedText);
        Assert.Equal(MovieEmbeddingContentBuilder.ComputeHash(stored.EmbeddedText), stored.ContentHash);
        Assert.NotEmpty(stored.Embedding);
        Assert.False(await db.MovieEmbeddingSyncStates.AnyAsync(item => item.MovieId == movie.Id));
    }

    [Fact]
    public async Task SyncMovieEmbeddingAsync_FailureCreatesPendingState_WithoutEmptyEmbedding()
    {
        await using var db = CreateDbContext();
        var movie = await SeedMovieAsync(db, "Pending movie", "Kinh dị");
        var embeddingService = new RecordingEmbeddingService
        {
            Handler = (_, _) => throw new HttpRequestException("Provider unavailable")
        };
        var service = CreateSyncService(db, embeddingService);

        var result = await service.SyncMovieEmbeddingAsync(movie.Id);

        Assert.Equal(MovieEmbeddingSyncStatuses.Pending, result.Status);
        Assert.False(await db.MovieEmbeddings.AnyAsync(item => item.MovieId == movie.Id));
        var state = await db.MovieEmbeddingSyncStates.SingleAsync(item => item.MovieId == movie.Id);
        Assert.Equal(MovieEmbeddingSyncStatuses.Pending, state.Status);
        Assert.Equal(0, state.AttemptCount);
        Assert.NotNull(state.NextAttemptAt);
        Assert.Equal(64, state.RequestedContentHash.Length);
    }

    [Fact]
    public async Task SyncMovieEmbeddingAsync_RetryStopsAtConfiguredMaximum()
    {
        await using var db = CreateDbContext();
        var movie = await SeedMovieAsync(db, "Retry movie", "Hành động");
        var embeddingService = new RecordingEmbeddingService
        {
            Handler = (_, _) => throw new HttpRequestException("Still unavailable")
        };
        var service = CreateSyncService(
            db,
            embeddingService,
            new MovieEmbeddingRetryOptions { MaxAttempts = 2, BaseDelaySeconds = 1, MaxDelaySeconds = 4 });

        await service.SyncMovieEmbeddingAsync(movie.Id);
        var firstRetry = await service.SyncMovieEmbeddingAsync(movie.Id, isRetryAttempt: true);
        var secondRetry = await service.SyncMovieEmbeddingAsync(movie.Id, isRetryAttempt: true);

        Assert.Equal(MovieEmbeddingSyncStatuses.Pending, firstRetry.Status);
        Assert.Equal(MovieEmbeddingSyncStatuses.Failed, secondRetry.Status);
        var state = await db.MovieEmbeddingSyncStates.SingleAsync(item => item.MovieId == movie.Id);
        Assert.Equal(2, state.AttemptCount);
        Assert.Null(state.NextAttemptAt);
    }

    [Fact]
    public async Task SyncMovieEmbeddingAsync_GenreChangeReplacesOldSemanticContent()
    {
        await using var db = CreateDbContext();
        var movie = await SeedMovieAsync(db, "Genre movie", "Kinh dị");
        var embeddingService = new RecordingEmbeddingService();
        var service = CreateSyncService(db, embeddingService);
        await service.SyncMovieEmbeddingAsync(movie.Id);

        var oldAssignments = await db.MovieGenres.Where(item => item.MovieId == movie.Id).ToListAsync();
        db.MovieGenres.RemoveRange(oldAssignments);
        var newGenre = new Genre { Name = "Tâm lí" };
        db.Genres.Add(newGenre);
        db.MovieGenres.Add(new MovieGenre { MovieId = movie.Id, GenreId = newGenre.Id });
        await db.SaveChangesAsync();

        var result = await service.SyncMovieEmbeddingAsync(movie.Id);

        Assert.Equal(MovieEmbeddingSyncStatuses.Ready, result.Status);
        Assert.Equal(2, embeddingService.CallCount);
        var stored = await db.MovieEmbeddings.SingleAsync(item => item.MovieId == movie.Id);
        Assert.Contains("Genres: Tâm lí", stored.EmbeddedText);
        Assert.DoesNotContain("Kinh dị", stored.EmbeddedText);
    }

    [Fact]
    public async Task SyncMovieEmbeddingAsync_DiscardsResultWhenMovieChangesDuringProviderCall()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = CreateOptions(databaseName);
        await using var syncDb = new AppDbContext(options);
        var movie = await SeedMovieAsync(syncDb, "Old title", "Kinh dị");
        var gatedEmbedding = new GatedEmbeddingService();
        var service = CreateSyncService(syncDb, gatedEmbedding);

        var syncTask = service.SyncMovieEmbeddingAsync(movie.Id);
        await gatedEmbedding.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await using (var updateDb = new AppDbContext(options))
        {
            var current = await updateDb.Movies.SingleAsync(item => item.Id == movie.Id);
            current.Title = "Newest title";
            await updateDb.SaveChangesAsync();
        }

        gatedEmbedding.Release.TrySetResult();
        var staleResult = await syncTask;

        Assert.Equal(MovieEmbeddingSyncStatuses.Pending, staleResult.Status);
        Assert.False(await syncDb.MovieEmbeddings.AnyAsync(item => item.MovieId == movie.Id));

        var recovered = await service.SyncMovieEmbeddingAsync(movie.Id);
        Assert.Equal(MovieEmbeddingSyncStatuses.Ready, recovered.Status);
        var stored = await syncDb.MovieEmbeddings.SingleAsync(item => item.MovieId == movie.Id);
        Assert.StartsWith("Newest title", stored.EmbeddedText);
    }

    [Fact]
    public async Task SyncMovieEmbeddingAsync_InactiveMovieRemovesEmbeddingAndRetryState()
    {
        await using var db = CreateDbContext();
        var movie = await SeedMovieAsync(db, "Inactive movie", "Kinh dị");
        var service = CreateSyncService(db, new RecordingEmbeddingService());
        await service.SyncMovieEmbeddingAsync(movie.Id);

        movie.Status = "Inactive";
        await db.SaveChangesAsync();
        var result = await service.SyncMovieEmbeddingAsync(movie.Id);

        Assert.Equal(MovieEmbeddingSyncStatuses.Ready, result.Status);
        Assert.False(await db.MovieEmbeddings.AnyAsync(item => item.MovieId == movie.Id));
        Assert.False(await db.MovieEmbeddingSyncStates.AnyAsync(item => item.MovieId == movie.Id));
    }

    [Fact]
    public async Task MovieService_CreateAndNonSemanticUpdate_ExposeReadyWithoutDuplicateEmbeddingCall()
    {
        await using var db = CreateDbContext();
        var genre = new Genre { Name = "Kinh dị" };
        db.Genres.Add(genre);
        await db.SaveChangesAsync();
        var embeddingService = new RecordingEmbeddingService();
        var syncService = CreateSyncService(db, embeddingService);
        var movieService = CreateMovieService(db, syncService);
        var dto = CreateMovieDto("Integrated movie", genre.Id);

        var created = await movieService.CreateAsync(dto);
        dto.PosterUrl = "new-poster.jpg";
        var updated = await movieService.UpdateWithEmbeddingAsync(created.Id, dto);

        Assert.Equal(MovieEmbeddingSyncStatuses.Ready, created.EmbeddingStatus);
        Assert.NotNull(updated);
        Assert.Equal(MovieEmbeddingSyncStatuses.Ready, updated!.EmbeddingStatus);
        Assert.Equal(1, embeddingService.CallCount);
        Assert.Equal("new-poster.jpg", (await db.Movies.SingleAsync(item => item.Id == created.Id)).PosterUrl);
    }

    [Fact]
    public async Task MovieService_CreatePersistsMovieAndReturnsPendingWhenProviderFails()
    {
        await using var db = CreateDbContext();
        var genre = new Genre { Name = "Kinh dị" };
        db.Genres.Add(genre);
        await db.SaveChangesAsync();
        var embeddingService = new RecordingEmbeddingService
        {
            Handler = (_, _) => throw new HttpRequestException("Provider unavailable")
        };
        var movieService = CreateMovieService(db, CreateSyncService(db, embeddingService));

        var created = await movieService.CreateAsync(CreateMovieDto("Saved while pending", genre.Id));

        Assert.Equal(MovieEmbeddingSyncStatuses.Pending, created.EmbeddingStatus);
        Assert.True(await db.Movies.AnyAsync(item => item.Id == created.Id));
        Assert.True(await db.MovieEmbeddingSyncStates.AnyAsync(item => item.MovieId == created.Id));
        Assert.False(await db.MovieEmbeddings.AnyAsync(item => item.MovieId == created.Id));
    }

    [Fact]
    public async Task MovieService_DeleteRemovesEmbeddingAndRetryState()
    {
        await using var db = CreateDbContext();
        var genre = new Genre { Name = "Kinh dị" };
        db.Genres.Add(genre);
        await db.SaveChangesAsync();
        var movieService = CreateMovieService(
            db,
            CreateSyncService(db, new RecordingEmbeddingService()));
        var created = await movieService.CreateAsync(CreateMovieDto("Delete movie", genre.Id));
        db.MovieEmbeddingSyncStates.Add(new MovieEmbeddingSyncState
        {
            MovieId = created.Id,
            Status = MovieEmbeddingSyncStatuses.Pending,
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(1)
        });
        await db.SaveChangesAsync();

        var deleted = await movieService.DeleteAsync(created.Id);

        Assert.True(deleted);
        Assert.False(await db.Movies.AnyAsync(item => item.Id == created.Id));
        Assert.False(await db.MovieEmbeddings.AnyAsync(item => item.MovieId == created.Id));
        Assert.False(await db.MovieEmbeddingSyncStates.AnyAsync(item => item.MovieId == created.Id));
    }

    [Fact]
    public async Task SemanticCatalogue_ExcludesEmbeddingWhileSyncStateExists()
    {
        await using var db = CreateDbContext();
        var movie = await SeedMovieAsync(db, "Stale candidate", "Kinh dị");
        db.MovieEmbeddings.Add(new MovieEmbedding
        {
            MovieId = movie.Id,
            EmbeddedText = "old",
            ContentHash = "old",
            Embedding = [1f, 0f, 0f],
            LastUpdatedAt = DateTime.UtcNow
        });
        db.MovieEmbeddingSyncStates.Add(new MovieEmbeddingSyncState
        {
            MovieId = movie.Id,
            Status = MovieEmbeddingSyncStatuses.Pending,
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(1)
        });
        await db.SaveChangesAsync();
        var queryEmbedding = new RecordingEmbeddingService
        {
            Handler = (_, _) => [1f, 0f, 0f]
        };
        var catalogue = new AssistantMovieCatalogue(db, queryEmbedding, new VectorSearchEngine());

        var results = await catalogue.SearchBySemanticQueryAsync("horror");

        Assert.Empty(results);
    }

    [Fact]
    public async Task RetryWorker_ProcessesDueStateAndMakesEmbeddingReady()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        var timeProvider = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var embeddingService = new RecordingEmbeddingService();
        var retryOptions = new MovieEmbeddingRetryOptions
        {
            BaseDelaySeconds = 1,
            MaxDelaySeconds = 4,
            MaxAttempts = 3,
            BatchSize = 5,
            PollIntervalSeconds = 1
        };

        services.AddLogging();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddSingleton<IEmbeddingService>(embeddingService);
        services.AddOptions<MovieEmbeddingRetryOptions>().Configure(options =>
        {
            options.BaseDelaySeconds = retryOptions.BaseDelaySeconds;
            options.MaxDelaySeconds = retryOptions.MaxDelaySeconds;
            options.MaxAttempts = retryOptions.MaxAttempts;
            options.BatchSize = retryOptions.BatchSize;
            options.PollIntervalSeconds = retryOptions.PollIntervalSeconds;
        });
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IEmbeddingSyncService, EmbeddingSyncService>();

        await using var provider = services.BuildServiceProvider();
        Guid movieId;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var movie = await SeedMovieAsync(db, "Retry worker movie", "Kinh dị");
            movieId = movie.Id;
            db.MovieEmbeddingSyncStates.Add(new MovieEmbeddingSyncState
            {
                MovieId = movieId,
                Status = MovieEmbeddingSyncStatuses.Pending,
                NextAttemptAt = timeProvider.GetUtcNow().AddSeconds(-1),
                RequestedContentHash = "stale"
            });
            await db.SaveChangesAsync();
        }

        var worker = new MovieEmbeddingRetryService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MovieEmbeddingRetryService>.Instance,
            Options.Create(retryOptions),
            timeProvider);
        var processed = await worker.ProcessDueRetriesAsync();

        Assert.Equal(1, processed);
        using var verificationScope = provider.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await verificationDb.MovieEmbeddings.AnyAsync(item => item.MovieId == movieId));
        Assert.False(await verificationDb.MovieEmbeddingSyncStates.AnyAsync(item => item.MovieId == movieId));
    }

    [Fact]
    public async Task FullSync_RepairsMissingEmbeddingAndCleansInactiveState()
    {
        await using var db = CreateDbContext();
        var activeMovie = await SeedMovieAsync(db, "Active movie", "Tâm lí");
        var inactiveMovie = await SeedMovieAsync(db, "Inactive legacy movie", "Kinh dị");
        inactiveMovie.Status = "Inactive";
        db.MovieEmbeddings.Add(new MovieEmbedding
        {
            MovieId = inactiveMovie.Id,
            EmbeddedText = "legacy",
            ContentHash = "legacy",
            Embedding = [0.1f, 0.2f, 0.3f],
            LastUpdatedAt = DateTime.UtcNow
        });
        db.MovieEmbeddingSyncStates.Add(new MovieEmbeddingSyncState
        {
            MovieId = activeMovie.Id,
            Status = MovieEmbeddingSyncStatuses.Pending,
            NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(1)
        });
        await db.SaveChangesAsync();
        var service = CreateSyncService(db, new RecordingEmbeddingService());

        await service.SyncMovieEmbeddingsAsync();

        Assert.True(await db.MovieEmbeddings.AnyAsync(item => item.MovieId == activeMovie.Id));
        Assert.False(await db.MovieEmbeddingSyncStates.AnyAsync(item => item.MovieId == activeMovie.Id));
        Assert.False(await db.MovieEmbeddings.AnyAsync(item => item.MovieId == inactiveMovie.Id));
    }

    private static EmbeddingSyncService CreateSyncService(
        AppDbContext db,
        IEmbeddingService embeddingService,
        MovieEmbeddingRetryOptions? retryOptions = null,
        TimeProvider? timeProvider = null)
    {
        return new EmbeddingSyncService(
            db,
            embeddingService,
            NullLogger<EmbeddingSyncService>.Instance,
            Options.Create(retryOptions ?? new MovieEmbeddingRetryOptions()),
            timeProvider ?? TimeProvider.System);
    }

    private static MovieService CreateMovieService(AppDbContext db, IEmbeddingSyncService syncService) =>
        new(
            db,
            CreateMapper(),
            syncService,
            Options.Create(new MovieEmbeddingRetryOptions()),
            TimeProvider.System);

    private static AppDbContext CreateDbContext() =>
        new(CreateOptions(Guid.NewGuid().ToString()));

    private static DbContextOptions<AppDbContext> CreateOptions(string databaseName) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

    private static async Task<Movie> SeedMovieAsync(
        AppDbContext db,
        string title,
        params string[] genreNames)
    {
        var movie = new Movie
        {
            Title = title,
            Description = "Description",
            Duration = 120,
            ReleaseDate = DateTime.UtcNow,
            Language = "Vietnamese",
            Rating = "T13",
            PosterUrl = "poster.jpg",
            Status = "NowShowing"
        };

        foreach (var genreName in genreNames)
        {
            var genre = new Genre { Name = genreName };
            movie.MovieGenres.Add(new MovieGenre
            {
                MovieId = movie.Id,
                GenreId = genre.Id,
                Movie = movie,
                Genre = genre
            });
        }

        db.Movies.Add(movie);
        await db.SaveChangesAsync();
        return movie;
    }

    private static MovieDto CreateMovieDto(string title, Guid genreId) => new()
    {
        Title = title,
        Description = "Description",
        Duration = 120,
        ReleaseDate = DateTime.UtcNow,
        Language = "Vietnamese",
        Rating = "T13",
        PosterUrl = "poster.jpg",
        Status = "NowShowing",
        GenreIds = [genreId]
    };

    private static IMapper CreateMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(config => config.AddProfile<EntityDtoProfile>());
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    private sealed class RecordingEmbeddingService : IEmbeddingService
    {
        public int CallCount { get; private set; }
        public Func<string, CancellationToken, float[]> Handler { get; init; } =
            (_, _) => [0.1f, 0.2f, 0.3f];

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Handler(text, cancellationToken));
        }

        public async Task<IReadOnlyList<float[]>> BatchEmbedAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            var results = new List<float[]>();
            foreach (var text in texts)
            {
                results.Add(await EmbedAsync(text, cancellationToken));
            }

            return results;
        }
    }

    private sealed class GatedEmbeddingService : IEmbeddingService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return [0.1f, 0.2f, 0.3f];
        }

        public Task<IReadOnlyList<float[]>> BatchEmbedAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<float[]>>([]);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount) => _utcNow = _utcNow.Add(amount);
    }
}
