namespace MovieBooking.Application.Common.Interfaces;

public interface IVectorSearchEngine
{
    IReadOnlyList<VectorSearchResult> Search(
        float[] query,
        IReadOnlyList<VectorSearchCandidate> candidates,
        int topK = 5,
        float threshold = 0.55f);
}

public sealed class VectorSearchCandidate
{
    public Guid Id { get; init; }
    public float[] Embedding { get; init; } = [];
    public string? Metadata { get; init; }
}

public sealed class VectorSearchResult
{
    public Guid Id { get; init; }
    public float Score { get; init; }
    public string? Metadata { get; init; }
}
