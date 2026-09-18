using MovieBooking.Application.Common.DTOs;

namespace MovieBooking.Application.Common.Interfaces;

public interface IEmbeddingSyncService
{
    Task SyncMovieEmbeddingsAsync(CancellationToken cancellationToken = default);
    Task<MovieEmbeddingSyncResult> SyncMovieEmbeddingAsync(
        Guid movieId,
        bool isRetryAttempt = false,
        CancellationToken cancellationToken = default);
    Task RemoveMovieEmbeddingAsync(Guid movieId, CancellationToken cancellationToken = default);
    Task SyncKnowledgeDocumentsAsync(CancellationToken cancellationToken = default);
}
