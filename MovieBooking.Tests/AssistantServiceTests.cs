using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieBooking.Application.Common.Configuration;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Controllers;
using MovieBooking.Infrastructure.Persistence;
using MovieBooking.Infrastructure.Services;
using Xunit;

namespace MovieBooking.Tests;

public sealed class AssistantServiceTests
{
    [Fact]
    public async Task Disabled_ReturnsUnavailableWithoutDependencies()
    {
        var catalogue = new FakeCatalogue();
        var client = new FakeClient();
        var result = await Service(catalogue, client, false).SendAsync(Request());
        Assert.Equal(AssistantResultKinds.Unavailable, result.Kind);
        Assert.False(result.Retryable);
        Assert.Equal(0, catalogue.Calls);
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task Grounding_DropsUnknownIdsAndUsesCatalogueFields()
    {
        var known = Guid.NewGuid();
        var catalogue = new FakeCatalogue { Movies = [Movie(known)] };
        var client = new FakeClient { Result = Result(AssistantResultKinds.GroundedResult, [known, Guid.NewGuid()]) };
        var response = await Service(catalogue, client, true).SendAsync(Request());
        var card = Assert.Single(response.Movies);
        Assert.Equal(known, card.Id);
        Assert.Equal("Grounded movie", card.Title);
    }

    [Fact]
    public async Task UnknownOnlyGrounding_BecomesNoResult()
    {
        var client = new FakeClient { Result = Result(AssistantResultKinds.GroundedResult, [Guid.NewGuid()]) };
        var response = await Service(new FakeCatalogue(), client, true).SendAsync(Request());
        Assert.Equal(AssistantResultKinds.NoResult, response.Kind);
        Assert.Empty(response.Movies);
    }

    [Fact]
    public async Task Clarification_HasNoCardsAndChoicesAreBounded()
    {
        var known = Guid.NewGuid();
        var client = new FakeClient
        {
            Result = new AiAssistantResult
            {
                Kind = AssistantResultKinds.Clarification, Text = "Which one?", MovieIds = [known],
                ClarificationChoices = ["1", "2", "3", "4", "5", "6"]
            }
        };
        var response = await Service(new FakeCatalogue { Movies = [Movie(known)] }, client, true).SendAsync(Request());
        Assert.Equal(AssistantResultKinds.Clarification, response.Kind);
        Assert.Empty(response.Movies);
        Assert.Equal(5, response.ClarificationChoices.Count);
    }

    [Fact]
    public async Task ProviderFailure_IsRetryableUnavailable()
    {
        var client = new FakeClient { Exception = new HttpRequestException("down") };
        var response = await Service(new FakeCatalogue(), client, true).SendAsync(Request());
        Assert.Equal(AssistantResultKinds.Unavailable, response.Kind);
        Assert.True(response.Retryable);
    }

    [Fact]
    public async Task ExcessiveHistory_IsRejectedBeforeDependencies()
    {
        var catalogue = new FakeCatalogue();
        var client = new FakeClient();
        var history = Enumerable.Range(0, 13).Select(_ => new AssistantMessageDto { Role = "user", Content = "x" }).ToList();
        await Assert.ThrowsAsync<ArgumentException>(() => Service(catalogue, client, true).SendAsync(Request(history)));
        Assert.Equal(0, catalogue.Calls);
        Assert.Equal(0, client.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyMessage_IsRejected(string message)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Service(new FakeCatalogue(), new FakeClient(), true)
            .SendAsync(new SendAssistantMessageRequestDto { Message = message }));
    }

    [Fact]
    public void ApiBoundary_RequiresAuthenticationAndRateLimit()
    {
        var type = typeof(AssistantController);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("Assistant", type.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
        Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    private static SendAssistantMessageRequestDto Request(IReadOnlyList<AssistantMessageDto>? history = null) => new()
    { Message = "Recommend an action movie", Locale = "en", History = history ?? [] };

    private static AssistantMovieCandidateDto Movie(Guid id) => new()
    { Id = id, Title = "Grounded movie", Description = "Description", Duration = 100, Rating = "T13", Status = "NowShowing", Genres = ["Action"] };

    private static AiAssistantResult Result(string kind, IReadOnlyList<Guid> ids) => new()
    { Kind = kind, Text = "Result", Language = "en", MovieIds = ids };

    private static AssistantService Service(FakeCatalogue catalogue, FakeClient client, bool enabled)
    {
        var options = Options.Create(new AssistantOptions { Enabled = enabled });
        var logger = NullLogger<AssistantService>.Instance;
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(dbOptions);
        return new AssistantService(catalogue, client, options, logger, db);
    }

    private sealed class FakeCatalogue : IAssistantMovieCatalogue
    {
        public int Calls { get; private set; }
        public IReadOnlyList<AssistantMovieCandidateDto> Movies { get; init; } = [];
        public Task<IReadOnlyList<AssistantMovieCandidateDto>> GetCandidatesAsync(CancellationToken cancellationToken = default)
        { Calls++; return Task.FromResult(Movies); }

        public Task<IReadOnlyList<AssistantMovieCandidateDto>> SearchBySemanticQueryAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
        { return Task.FromResult(Movies); }

        public Task<IReadOnlyList<AssistantKnowledgeDocumentDto>> SearchKnowledgeDocumentsAsync(string query, int topK = 3, CancellationToken cancellationToken = default)
        { return Task.FromResult<IReadOnlyList<AssistantKnowledgeDocumentDto>>([]); }
    }

    [Fact]
    public async Task HybridRag_PassesShowtimesWithMovieTitleAndContextToAiClient()
    {
        var cinema = new Domain.Entities.Cinema { Name = "CGV Vincom", Address = "191 Ba Trieu", City = "Ha Noi" };
        var room = new Domain.Entities.Room { Name = "Room 1", Type = "2D", Cinema = cinema };
        var movie = new Domain.Entities.Movie { Title = "Dune: Part Two", Description = "Epic sci-fi", Duration = 166, Rating = "T16", Status = "NowShowing" };
        var movieId = movie.Id;
        var showtime = new Domain.Entities.Showtime
        {
            MovieId = movieId,
            Movie = movie,
            Room = room,
            StartTime = DateTime.UtcNow.AddHours(2),
            EndTime = DateTime.UtcNow.AddHours(5),
            BasePrice = 90000,
            Status = "Active"
        };

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(dbOptions);
        db.Movies.Add(movie);
        db.Cinemas.Add(cinema);
        db.Rooms.Add(room);
        db.Showtimes.Add(showtime);
        await db.SaveChangesAsync();

        var catalogue = new FakeCatalogue
        {
            Movies = [new AssistantMovieCandidateDto
            {
                Id = movieId, Title = "Dune: Part Two", Description = "Epic sci-fi",
                Duration = 166, Rating = "T16", Status = "NowShowing", Genres = ["Sci-Fi"]
            }]
        };

        var client = new FakeClient
        {
            Result = new AiAssistantResult
            {
                Kind = AssistantResultKinds.GroundedResult,
                Text = "Dune is showing tonight.",
                Language = "vi",
                MovieIds = [movieId]
            }
        };

        var options = Options.Create(new AssistantOptions { Enabled = true });
        var service = new AssistantService(catalogue, client, options, NullLogger<AssistantService>.Instance, db);

        var response = await service.SendAsync(Request());

        Assert.NotNull(client.LastRequest);
        Assert.Contains("Phim: Dune: Part Two", client.LastRequest.Context);
        Assert.Contains("Rạp: CGV Vincom", client.LastRequest.Context);
        Assert.Contains("Phòng: Room 1 (2D)", client.LastRequest.Context);
        Assert.Contains("90,000 VNĐ", client.LastRequest.Context);
        Assert.Single(client.LastRequest.Movies);
        Assert.Equal(movieId, client.LastRequest.Movies[0].Id);
    }

    private sealed class FakeClient : IAiAssistantClient
    {
        public int Calls { get; private set; }
        public AiAssistantRequest? LastRequest { get; private set; }
        public AiAssistantResult Result { get; init; } = new() { Kind = AssistantResultKinds.NoResult, Text = "No result" };
        public Exception? Exception { get; init; }
        public Task<AiAssistantResult> CompleteAsync(AiAssistantRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            if (Exception is not null) throw Exception;
            return Task.FromResult(Result);
        }
    }
}
