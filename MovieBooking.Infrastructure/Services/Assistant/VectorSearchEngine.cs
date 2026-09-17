using System.Numerics.Tensors;
using MovieBooking.Application.Common.Interfaces;

namespace MovieBooking.Infrastructure.Services.Assistant;

public sealed class VectorSearchEngine : IVectorSearchEngine
{
    public IReadOnlyList<VectorSearchResult> Search(
        float[] query,
        IReadOnlyList<VectorSearchCandidate> candidates,
        int topK = 5,
        float threshold = 0.55f)
    {
        if (query.Length == 0 || candidates.Count == 0)
            return [];

        var results = new List<VectorSearchResult>(candidates.Count);

        foreach (var candidate in candidates)
        {
            if (candidate.Embedding.Length == 0 || candidate.Embedding.Length != query.Length)
                continue;

            var score = TensorPrimitives.CosineSimilarity(query.AsSpan(), candidate.Embedding.AsSpan());

            if (score >= threshold)
            {
                results.Add(new VectorSearchResult
                {
                    Id = candidate.Id,
                    Score = score,
                    Metadata = candidate.Metadata
                });
            }
        }

        return results
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }
}
