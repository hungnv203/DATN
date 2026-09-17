using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Infrastructure.Services.Assistant;
using Xunit;

namespace MovieBooking.Tests;

public sealed class VectorSearchEngineTests
{
    private readonly VectorSearchEngine _engine = new();

    [Fact]
    public void Search_PerfectMatch_ReturnsHighScore()
    {
        var query = new float[] { 1f, 0f, 0f };
        var candidates = new List<VectorSearchCandidate>
        {
            new() { Id = Guid.NewGuid(), Embedding = new float[] { 1f, 0f, 0f } }
        };

        var results = _engine.Search(query, candidates);

        Assert.Single(results);
        Assert.Equal(1f, results[0].Score, 4);
    }

    [Fact]
    public void Search_OrthogonalVectors_ReturnsLowScore()
    {
        var query = new float[] { 1f, 0f, 0f };
        var candidates = new List<VectorSearchCandidate>
        {
            new() { Id = Guid.NewGuid(), Embedding = new float[] { 0f, 1f, 0f } }
        };

        var results = _engine.Search(query, candidates);

        Assert.Empty(results);
    }

    [Fact]
    public void Search_SimilarVectors_ReturnsMiddleScore()
    {
        var query = new float[] { 1f, 0f, 0f };
        var candidates = new List<VectorSearchCandidate>
        {
            new() { Id = Guid.NewGuid(), Embedding = new float[] { 0.8f, 0.6f, 0f } }
        };

        var results = _engine.Search(query, candidates, threshold: 0.5f);

        Assert.Single(results);
        Assert.InRange(results[0].Score, 0.5f, 1f);
    }

    [Fact]
    public void Search_TopK_ReturnsLimitedResults()
    {
        var query = new float[] { 1f, 0f, 0f };
        var candidates = new List<VectorSearchCandidate>
        {
            new() { Id = Guid.NewGuid(), Embedding = new float[] { 1f, 0f, 0f } },
            new() { Id = Guid.NewGuid(), Embedding = new float[] { 0.9f, 0.1f, 0f } },
            new() { Id = Guid.NewGuid(), Embedding = new float[] { 0.8f, 0.2f, 0f } },
            new() { Id = Guid.NewGuid(), Embedding = new float[] { 0.7f, 0.3f, 0f } },
            new() { Id = Guid.NewGuid(), Embedding = new float[] { 0.6f, 0.4f, 0f } },
            new() { Id = Guid.NewGuid(), Embedding = new float[] { 0.5f, 0.5f, 0f } }
        };

        var results = _engine.Search(query, candidates, topK: 3, threshold: 0.4f);

        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Search_OrdersByScoreDescending()
    {
        var query = new float[] { 1f, 0f, 0f };
        var candidates = new List<VectorSearchCandidate>
        {
            new() { Id = Guid.NewGuid(), Embedding = new float[] { 0.6f, 0.4f, 0f } },
            new() { Id = Guid.NewGuid(), Embedding = new float[] { 0.9f, 0.1f, 0f } },
            new() { Id = Guid.NewGuid(), Embedding = new float[] { 0.7f, 0.3f, 0f } }
        };

        var results = _engine.Search(query, candidates, threshold: 0.4f);

        Assert.Equal(3, results.Count);
        Assert.True(results[0].Score >= results[1].Score);
        Assert.True(results[1].Score >= results[2].Score);
    }

    [Fact]
    public void Search_EmptyQuery_ReturnsEmpty()
    {
        var query = Array.Empty<float>();
        var candidates = new List<VectorSearchCandidate>
        {
            new() { Id = Guid.NewGuid(), Embedding = new float[] { 1f, 0f, 0f } }
        };

        var results = _engine.Search(query, candidates);

        Assert.Empty(results);
    }

    [Fact]
    public void Search_EmptyCandidates_ReturnsEmpty()
    {
        var query = new float[] { 1f, 0f, 0f };
        var candidates = new List<VectorSearchCandidate>();

        var results = _engine.Search(query, candidates);

        Assert.Empty(results);
    }

    [Fact]
    public void Search_DifferentDimensions_SkipsCandidate()
    {
        var query = new float[] { 1f, 0f, 0f };
        var candidates = new List<VectorSearchCandidate>
        {
            new() { Id = Guid.NewGuid(), Embedding = new float[] { 1f, 0f } }
        };

        var results = _engine.Search(query, candidates);

        Assert.Empty(results);
    }

    [Fact]
    public void Search_WithMetadata_PreservesMetadata()
    {
        var query = new float[] { 1f, 0f, 0f };
        var candidates = new List<VectorSearchCandidate>
        {
            new() { Id = Guid.NewGuid(), Embedding = new float[] { 1f, 0f, 0f }, Metadata = "Test Movie" }
        };

        var results = _engine.Search(query, candidates);

        Assert.Single(results);
        Assert.Equal("Test Movie", results[0].Metadata);
    }
}
