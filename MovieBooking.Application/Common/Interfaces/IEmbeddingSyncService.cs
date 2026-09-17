namespace MovieBooking.Application.Common.Interfaces;

public interface IEmbeddingSyncService
{
    Task SyncMovieEmbeddingsAsync(CancellationToken cancellationToken = default);
    Task SyncKnowledgeDocumentsAsync(CancellationToken cancellationToken = default);
}
