using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MovieBooking.Application.Common.Configuration;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Infrastructure.Services;
using Xunit;

namespace MovieBooking.Tests;

public sealed class AssistantServiceTests
{
    [Fact]
    public async Task DisabledAssistant_ReturnsUnavailableWithoutCallingDependencies()
    {
        var catalogue = new FakeCatalogue();
        var client = new FakeClient();
        var service = CreateService(catalogue, client, enabled: false);

        var result = await service.SendAsync(new SendAssistantMessageRequestDto
        {
            Message = "Recommend a movie",
            Locale = "en"
        });

        Assert.Equal(AssistantResultKinds.Unavailable, result.Kind);
        Assert.False(result.Retryable);
        Assert.Equal(0, catalogue.CallCount);
        Assert.Equal(0, client.CallCount);
    }

    [Fact]
    public async Task GroundedResult_DropsUnknownMovieIdsAndBuildsCardsFromCatalogue()
    {
        var knownId = Guid.NewGuid();
        var catalogue = new FakeCatalogue
        {
            Movies =
            [
                new AssistantMovieCandidateDto
                {
                    Id = knownId,
                    Title = "Grounded movie",
                    Description = "Catalogue description",
                    Duration = 110,
                    Rating = "T13",
                    Status = "NowShowing",
                    Genres = ["Action"]
                }
            ]
        };
        var client = new FakeClient
        {
            Result = new AiAssistantResult
            {
                Kind = AssistantResultKinds.GroundedResult,
                Text = "One grounded result.",
                Language = "en",
                MovieIds = [knownId, Guid.NewGuid()],
                Reasons = new Dictionary<Guid, string> { [knownId] = "Matches action." }
            }
        };
        var service = CreateService(catalogue, client, enabled: true);

        var result = await service.SendAsync(new SendAssistantMessageRequestDto
        {
            Message = "Recommend action",
            Locale = "en"
        });

        var card = Assert.Single(result.Movies);
        Assert.Equal(knownId, card.Id);
        Assert.Equal("Grounded movie", card.Title);
        Assert.Equal("Matches action.", card.Reason);
    }

    [Fact]
    public async Task ExcessiveHistory_IsRejectedBeforeProviderCall()
    {
        var client = new FakeClient();
        var service = CreateService(new FakeCatalogue(), client, enabled: true);
        var history = Enumerable.Range(0, 13)
            .Select(_ => new AssistantMessageDto { Role = "user", Content = "message" })
            .ToList();

        await Assert.ThrowsAsync<ArgumentException>(() => service.SendAsync(
            new SendAssistantMessageRequestDto
            {
                Message = "latest",
                History = history
            }));
        Assert.Equal(0, client.CallCount);
    }

    private static AssistantService CreateService(
        FakeCatalogue catalogue,
        FakeClient client,
        bool enabled)
    {
        return new AssistantService(
            catalogue,
            client,
            Options.Create(new AssistantOptions { Enabled = enabled }),
            NullLogger<AssistantService>.Instance);
    }

    private sealed class FakeCatalogue : IAssistantMovieCatalogue
    {
        public int CallCount { get; private set; }
        public IReadOnlyList<AssistantMovieCandidateDto> Movies { get; init; } = [];

        public Task<IReadOnlyList<AssistantMovieCandidateDto>> GetCandidatesAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Movies);
        }
    }

    private sealed class FakeClient : IAiAssistantClient
    {
        public int CallCount { get; private set; }
        public AiAssistantResult Result { get; init; } = new()
        {
            Kind = AssistantResultKinds.NoResult,
            Text = "No result."
        };

        public Task<AiAssistantResult> CompleteAsync(
            AiAssistantRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Result);
        }
    }
}
