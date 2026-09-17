namespace MovieBooking.Application.Common.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<float[]>> BatchEmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
